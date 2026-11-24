using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DataLoaderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public class TokenRequest
        {
            public string ClientId { get; set; } = string.Empty;
            public string ClientSecret { get; set; } = string.Empty;
        }

        [HttpPost("token")]
        public IActionResult GenerateToken([FromBody] TokenRequest request)
        {
            var clientId = Request.Form["client_id"].FirstOrDefault();
            var clientSecret = Request.Form["client_secret"].FirstOrDefault();

            if (string.IsNullOrEmpty(clientId)){}

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var validClientId = jwtSettings["ClientId"];
            var validClientSecret = jwtSettings["ClientSecret"];

            // 1. Validate the credentials provided by Salesforce
            if (clientId != validClientId || clientSecret != validClientSecret)
            {
                return Unauthorized(new { error = "invalid_client", error_description = "Invalid credentials" });
            }

            // 2. Generate the JWT
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, request.ClientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1), // Token is valid for 1 hour
                signingCredentials: credentials);

            return Ok(new { 
                access_token = new JwtSecurityTokenHandler().WriteToken(token),
                token_type = "Bearer",
                expires_in = 3600
            });
        }
    }
}