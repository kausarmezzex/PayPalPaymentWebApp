using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PayPalPaymentWebApp.Models;

public class PayPalService
{
    private readonly string _clientId;
    private readonly string _secret;
    private readonly HttpClient _httpClient;

    public PayPalService(string clientId, string secret)
    {
        _clientId = clientId;
        _secret = secret;
        _httpClient = new HttpClient();
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_secret}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" }
        };

        try
        {
            var response = await _httpClient.PostAsync("https://api.sandbox.paypal.com/v1/oauth2/token", new FormUrlEncodedContent(requestBody));
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<PayPalTokenResponse>(responseContent);

            return tokenResponse.access_token;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Invalid PayPal credentials. Please verify your client ID and secret.", ex);
        }
    }

    public async Task<PayPalPaymentResponse> CreatePaymentAsync(string accessToken, object paymentRequest)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var content = new StringContent(JsonConvert.SerializeObject(paymentRequest), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.sandbox.paypal.com/v1/payments/payment", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<PayPalPaymentResponse>(responseContent);
    }
}



public class PayPalTokenResponse
{
    public string scope { get; set; }
    public string access_token { get; set; }
    public string token_type { get; set; }
    public int expires_in { get; set; }
    public string app_id { get; set; }
}
