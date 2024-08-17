using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PayPalPaymentWebApp.Data;
using PayPalPaymentWebApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PayPalPaymentWebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PayPalService _payPalService;
        private readonly IConfiguration _configuration;

        public AccountController(ApplicationDbContext context, PayPalService payPalService, IConfiguration configuration)
        {
            _context = context;
            _payPalService = payPalService;
            _configuration = configuration;
            ViewData["RemainingTickets"] = GetRemainingTickets();
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewData["RemainingTickets"] = GetRemainingTickets();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model, string confirmPassword)
        {
            if (ModelState.IsValid)
            {
                if (model.PasswordHash != confirmPassword)
                {
                    ModelState.AddModelError("", "Passwords do not match.");
                    return View(model);
                }

                // Check ticket availability
                int maxTickets = int.Parse(_configuration["TicketSettings:TotalTickets"]);
                int currentTicketCount = await _context.PaymentTokens.CountAsync();
                int remainingTickets = maxTickets - currentTicketCount;

                if (model.NumberOfTickets > remainingTickets)
                {
                    ViewData["RemainingTickets"] = GetRemainingTickets();
                    ModelState.AddModelError("", $"You are trying to book {model.NumberOfTickets} tickets, but only {remainingTickets} tickets are available.");
                    return View(model);
                }

                // Hash the password before saving
                model.PasswordHash = HashPassword(model.PasswordHash);

                // Save user details to the database
                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                // Redirect to the payment process, passing the user model and number of tickets
                return RedirectToAction("ProcessPayment", new { userId = model.UserId });
            }

            return View(model);
        }


        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public async Task<IActionResult> ProcessPayment(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    return View("Error");
                }

                // Calculate the total amount based on the number of tickets
                decimal ticketPrice = decimal.Parse(_configuration["TicketSettings:TicketPrice"]);
                decimal totalAmount = ticketPrice * user.NumberOfTickets;

                var accessToken = await _payPalService.GetAccessTokenAsync();
                var paymentResponse = await CreatePayPalPayment(accessToken, user, totalAmount);

                if (paymentResponse != null)
                {
                    var approvalUrl = paymentResponse.GetApprovalUrl();
                    return Redirect(approvalUrl);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing payment: {ex.Message}");
                return View("Error");
            }

            return View("Error");
        }

        public async Task<IActionResult> PaymentSuccess(int userId, string paymentId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View("Error");
            }

            // Check if payment has already been processed
            if (await _context.PaymentTokens.AnyAsync(t => t.PaymentId == paymentId))
            {
                ViewData["RemainingTickets"] = GetRemainingTickets();
                return View("Success", await _context.PaymentTokens.Where(t => t.PaymentId == paymentId).Select(t => t.Token).ToListAsync());
            }

            int maxTickets = int.Parse(_configuration["TicketSettings:TotalTickets"]);
            int currentTicketCount = await _context.PaymentTokens.CountAsync();

            if (currentTicketCount + user.NumberOfTickets > maxTickets)
            {
                ModelState.AddModelError("", "Not enough tickets left.");
                return View("Error");
            }

            var lastToken = await _context.PaymentTokens
                                          .OrderByDescending(t => t.TokenId)
                                          .Select(t => t.Token)
                                          .FirstOrDefaultAsync();

            int startTokenNumber = 1000;
            if (!string.IsNullOrEmpty(lastToken) && lastToken.StartsWith("SHA"))
            {
                int lastTokenNumber = int.Parse(lastToken.Substring(3));
                startTokenNumber = lastTokenNumber + 1;
            }

            List<PaymentToken> paymentTokens = new List<PaymentToken>();
            for (int i = 0; i < user.NumberOfTickets; i++)
            {
                string token = "SHA" + (startTokenNumber + i).ToString();
                paymentTokens.Add(new PaymentToken
                {
                    UserId = userId,
                    Token = token,
                    PaymentDate = DateTime.UtcNow,
                    PaymentId = paymentId // Save the payment ID here
                });
            }

            try
            {
                _context.PaymentTokens.AddRange(paymentTokens);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving payment tokens: {ex.Message}");
                return View("Error");
            }

            var tokenStrings = paymentTokens.Select(t => t.Token).ToList();
            ViewData["RemainingTickets"] = GetRemainingTickets();
            return View("Success", tokenStrings);
        }




        private async Task<PayPalPaymentResponse> CreatePayPalPayment(string accessToken, User user, decimal totalAmount)
        {
            var paymentRequest = new
            {
                intent = "sale",
                payer = new { payment_method = "paypal" },
                transactions = new[]
                {
                new
                {
                    amount = new { total = totalAmount.ToString("F2"), currency = "USD" },
                    description = "Registration Fee"
                }
            },
                redirect_urls = new
                {
                    return_url = Url.Action("PaymentSuccess", "Account", new { userId = user.UserId }, protocol: Request.Scheme),
                    cancel_url = Url.Action("PaymentCancelled", "Account", new { userId = user.UserId }, protocol: Request.Scheme)
                }
            };

            var paymentResponse = await _payPalService.CreatePaymentAsync(accessToken, paymentRequest);
            return paymentResponse;
        }

        protected int GetRemainingTickets()
        {
            int maxTickets = int.Parse(_configuration["TicketSettings:TotalTickets"]);
            int currentTicketCount = _context.PaymentTokens.Count();
            return maxTickets - currentTicketCount;
        }
    }

}
