using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileExchanger.Requests;
using FileExchanger.Responses;
using FileExchanger.Services.Encryptor;
using Microsoft.AspNetCore.Mvc;

namespace FileExchanger.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IEncryptorService _encryptorService;

        public AccountController(IEncryptorService encryptorService)
        {
            _encryptorService = encryptorService;
        }
        [HttpPost]
        public IActionResult Login(LoginRequest request)
        {
            var keys = _encryptorService.GetKeys(request.Password);
            return Ok(new LoginResponse
            {
                PublicKey = keys.PublicKey
            });
        }
    }
}
