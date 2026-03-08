using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;
using webTFGBack.Models;

namespace LoginApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            Console.WriteLine($"Correo recibido: '{request.corrro}'");
            Console.WriteLine($"Pass recibido: '{request.pass}'");

            var cliente = await _context.Cliente
                .FirstOrDefaultAsync(c => c.correo == request.correo);

            if (cliente == null)
                return Unauthorized(new { message = "Usuario no encontrado" });

            if (cliente.pass != request.pass)
                return Unauthorized(new { message = "Contraseña incorrecta" });

            return Ok(new { message = "Login correcto", user = cliente.nombre });
        }
    }

    public class LoginRequest
    {
        public string correo { get; set; } = string.Empty;
        public string pass { get; set; } = string.Empty;
    }
}