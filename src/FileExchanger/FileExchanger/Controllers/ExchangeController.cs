using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FileExchanger.Requests;
using FileExchanger.Services.Encryptor;
using Microsoft.AspNetCore.Mvc;

namespace FileExchanger.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExchangeController : ControllerBase
    {
        private readonly IEncryptorService _encryptorService;

        public ExchangeController(IEncryptorService encryptorService)
        {
            _encryptorService = encryptorService;
        }

        [HttpGet("key")]
        public IActionResult GetSessionKey()
        {
            var sessionKey = _encryptorService.GenerateSessionKey();
            _encryptorService.SetSessionKey(sessionKey.ToList());
            var encryptedKey = _encryptorService.GetSessionKeyEncryptedWithReceiverPublicKey();
            return Ok(encryptedKey);
        }

        [HttpPost("packageText")]
        public async Task<IActionResult> PackageText(PackageTextRequest request)
        {
            var textBytes = Encoding.UTF8.GetBytes(request.Text);
            var packages = await _encryptorService.GetEncryptedPackagesAsync(textBytes, request.Mode);
            return Ok(packages);
        }

        [HttpPost("packageFile")]
        public async Task<IActionResult> PackageFile()
        {
            try
            {
                var formCollection = await Request.ReadFormAsync();
                var file = formCollection.Files.First();
                if (!formCollection.TryGetValue("mode", out var modeValue) ||
                    !Enum.TryParse(typeof(CipherMode), modeValue.ToString(), true, out var mode))
                {
                    return BadRequest();
                }

                await using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                var packages = await _encryptorService.GetEncryptedPackagesAsync(fileBytes, (CipherMode)mode);
                return Ok(packages);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }
        }
    }
}
