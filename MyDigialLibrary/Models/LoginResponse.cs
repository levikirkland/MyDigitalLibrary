using MyDigitalLibrary.Models;

namespace MyDigitalLibrary.Models
{
    public class LoginResponse
    {
        public string Token { get; set; }
        public string Message { get; set; }
        public User User { get; set; }
    }
}
