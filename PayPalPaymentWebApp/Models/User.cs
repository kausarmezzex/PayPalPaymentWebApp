namespace PayPalPaymentWebApp.Models
{
    public class User
    {
        public int UserId { get; set; }  // Unique identifier for the user
        public string FirstName { get; set; }  // First name of the user
        public string LastName { get; set; }  // Last name of the user
        public string Email { get; set; }  // Email address of the user
        public string PasswordHash { get; set; }  // Hashed password for security
        public int Age { get; set; }  // Age of the user
        public string PhoneNumber { get; set; }  // Phone number of the user

        // New Fields
        public string Title { get; set; }  // Title (e.g., Mr., Mrs., Dr., etc.)
        public string Gender { get; set; }
        public int NumberOfTickets { get; set; }  // Number of tickets required
        public string CountryOfBirth { get; set; }  // Country of birth of the user

        // Address Information
        public string Address1 { get; set; }  // Address Line 1
        public string Address2 { get; set; }  // Address Line 2
        public string City { get; set; }  // City
        public string PostCode { get; set; }  // Post Code
    }
}
