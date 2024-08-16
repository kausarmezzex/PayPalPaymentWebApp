using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        public int GetRemainingTickets()
        {
            int maxTickets = int.Parse(_configuration["TicketSettings:TotalTickets"]);
            int currentTicketCount = _context.PaymentTokens.Count();
            return maxTickets - currentTicketCount;
        }


        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model, string confirmPassword)
        {
            if (ModelState.IsValid)
            {
                if (model.PasswordHash != confirmPassword)
                {
                    ModelState.AddModelError("", "Passwords do not match");
                    return View(model);
                }

                // Hash the password before saving using BCrypt
                model.PasswordHash = HashPassword(model.PasswordHash);

                // Save user details to the database
                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                // Redirect to the payment process, passing the user model
                return RedirectToAction("ProcessPayment", new { userId = model.UserId });
            }
            return View(model);
        }

        private string HashPassword(string password)
        {
            // Using BCrypt to securely hash passwords
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public async Task<IActionResult> ProcessPayment(int userId)
        {
            try
            {
                // Retrieve the user's information from the database
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    // If the user does not exist, return an error view or a not found result
                    ModelState.AddModelError("", "User not found.");
                    return View("Error");
                }

                // Get the PayPal access token
                var accessToken = await _payPalService.GetAccessTokenAsync();

                // Use the access token to create a payment request to PayPal
                var paymentResponse = await CreatePayPalPayment(accessToken, user);

                // Handle payment creation response and redirect to PayPal for approval
                if (paymentResponse != null)
                {
                    // Extract the approval URL from PayPal's response
                    var approvalUrl = paymentResponse.GetApprovalUrl();
                    return Redirect(approvalUrl);
                }
            }
            catch (Exception ex)
            {
                // Log the error and redirect to an error view
                Console.WriteLine($"Error processing payment: {ex.Message}");
                return View("Error");
            }

            // Handle error case (e.g., failed to create payment)
            return View("Error");
        }

        public async Task<IActionResult> PaymentSuccess(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View("Error");
            }

            // Get the maximum number of tickets allowed from configuration
            int maxTickets = int.Parse(_configuration["TicketSettings:TotalTickets"]);

            // Get the current count of generated tickets
            int currentTicketCount = await _context.PaymentTokens.CountAsync();

            // Check if generating new tickets would exceed the maximum
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
                    PaymentDate = DateTime.UtcNow
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
            return View("Success", tokenStrings);
        }





        public IActionResult PaymentCancelled(User model)
        {
            // Redirect back to the Register view with the previously entered data
            ModelState.AddModelError("", "Payment was cancelled. Please try again.");
            return View("Register", model);
        }

        private async Task<PayPalPaymentResponse> CreatePayPalPayment(string accessToken, User model)
        {
            // Define your payment request payload
            var paymentRequest = new
            {
                intent = "sale",
                payer = new { payment_method = "paypal" },
                transactions = new[]
                {
                    new
                    {
                        amount = new { total = "10.00", currency = "USD" },
                        description = "Registration Fee"
                    }
                },
                redirect_urls = new
                {
                    return_url = Url.Action("PaymentSuccess", "Account", new { userId = model.UserId }, protocol: Request.Scheme),
                    cancel_url = Url.Action("PaymentCancelled", "Account", new { userId = model.UserId }, protocol: Request.Scheme)
                }
            };

            // Make the request to PayPal
            var paymentResponse = await _payPalService.CreatePaymentAsync(accessToken, paymentRequest);

            return paymentResponse;
        }
    }
}
