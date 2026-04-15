using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;
using webTFGBack.Models;

namespace webTFGBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClienteController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ClienteController(AppDbContext context) => _context = context;

        // GET api/cliente/{id}  — perfil completo para el modal
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cliente = await _context.Cliente
                .Include(c => c.Persona)
                .Include(c => c.Suscripciones).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(c => c.id_cliente == id);

            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            return Ok(new
            {
                id_cliente = cliente.id_cliente,
                nombre = cliente.Persona!.nombre,
                email = cliente.Persona!.email,
                documento = cliente.Persona!.documento_identidad,
                telefono = cliente.Persona!.telefono,
                suscripciones = cliente.Suscripciones
                    .OrderByDescending(s => s.fecha_inicio)
                    .Select(s => new
                    {
                        id = s.id_suscripcion,
                        plan = s.Plan!.nombre,
                        precio = s.Plan!.precio,
                        fecha_inicio = s.fecha_inicio.ToString("dd/MM/yyyy"),
                        fecha_fin = s.fecha_fin.ToString("dd/MM/yyyy"),
                        accesos = s.accesos_restantes,
                        estado = s.estado,
                        dias_restantes = s.fecha_fin >= hoy ? s.fecha_fin.DayNumber - hoy.DayNumber : 0
                    })
                    .ToList()
            });
        }

        // GET api/cliente/buscar?q=texto&id_gym=1
        [HttpGet("buscar")]
        public async Task<IActionResult> Buscar([FromQuery] string q, [FromQuery] int? id_gym)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { message = "Parámetro de búsqueda vacío" });

            int? id_compania = null;
            if (id_gym.HasValue)
            {
                var gym = await _context.Gym.FindAsync(id_gym.Value);
                id_compania = gym?.id_compania;
            }

            var query = _context.Cliente
                .Include(c => c.Persona)
                .Include(c => c.Suscripciones).ThenInclude(s => s.Plan)
                .Where(c =>
                    c.Persona!.nombre.Contains(q) ||
                    c.Persona!.documento_identidad.Contains(q) ||
                    (c.Persona!.email != null && c.Persona.email.Contains(q)));

            if (id_compania.HasValue)
                query = query.Where(c => c.Suscripciones.Any(s =>
                    s.estado == "activa" && s.Plan!.id_compania == id_compania.Value));

            var resultados = await query
                .Select(c => new
                {
                    id_cliente = c.id_cliente,
                    nombre = c.Persona!.nombre,
                    email = c.Persona!.email,
                    documento = c.Persona!.documento_identidad,
                    telefono = c.Persona!.telefono,
                    plan_activo = c.Suscripciones
                        .Where(s => s.estado == "activa")
                        .OrderByDescending(s => s.fecha_inicio)
                        .Select(s => s.Plan!.nombre)
                        .FirstOrDefault()
                })
                .Take(10)
                .ToListAsync();

            return Ok(resultados);
        }

        // POST api/cliente
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] CrearClienteRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.pass))
                return BadRequest(new { message = "La contraseña es obligatoria" });

            bool existe = await _context.Persona
                .AnyAsync(p => p.documento_identidad == req.documento_identidad ||
                               (req.email != null && p.email == req.email));
            if (existe)
                return Conflict(new { message = "Ya existe una persona con ese documento o email" });

            var persona = new Persona
            {
                nombre = req.nombre,
                email = req.email,
                documento_identidad = req.documento_identidad,
                telefono = req.telefono,
                pass = req.pass
            };
            _context.Persona.Add(persona);
            await _context.SaveChangesAsync();

            var cliente = new Cliente { id_persona = persona.id_persona };
            _context.Cliente.Add(cliente);
            await _context.SaveChangesAsync();

            if (req.id_plan.HasValue)
            {
                var plan = await _context.Plan.FindAsync(req.id_plan.Value);
                if (plan != null)
                {
                    _context.Suscripcion.Add(new Suscripcion
                    {
                        id_cliente = cliente.id_cliente,
                        id_plan = plan.id_plan,
                        fecha_inicio = DateOnly.FromDateTime(DateTime.Today),
                        fecha_fin = DateOnly.FromDateTime(DateTime.Today.AddDays(plan.duracion_dias)),
                        accesos_restantes = plan.total_accesos,
                        estado = "activa"
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { message = "Cliente creado correctamente", id_cliente = cliente.id_cliente });
        }

        // PUT api/cliente/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Modificar(int id, [FromBody] ModificarClienteRequest req)
        {
            var cliente = await _context.Cliente
                .Include(c => c.Persona)
                .FirstOrDefaultAsync(c => c.id_cliente == id);

            if (cliente == null)
                return NotFound(new { message = "Cliente no encontrado" });

            if (!string.IsNullOrWhiteSpace(req.nombre)) cliente.Persona!.nombre = req.nombre;
            if (!string.IsNullOrWhiteSpace(req.email)) cliente.Persona!.email = req.email;
            if (!string.IsNullOrWhiteSpace(req.telefono)) cliente.Persona!.telefono = req.telefono;
            if (!string.IsNullOrWhiteSpace(req.pass)) cliente.Persona!.pass = req.pass;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cliente actualizado" });
        }

        // POST api/cliente/{id}/renovar
        [HttpPost("{id}/renovar")]
        public async Task<IActionResult> Renovar(int id, [FromBody] RenovarRequest req)
        {
            var cliente = await _context.Cliente.FindAsync(id);
            if (cliente == null) return NotFound(new { message = "Cliente no encontrado" });

            var plan = await _context.Plan.FindAsync(req.id_plan);
            if (plan == null) return NotFound(new { message = "Plan no encontrado" });

            _context.Suscripcion.Add(new Suscripcion
            {
                id_cliente = cliente.id_cliente,
                id_plan = plan.id_plan,
                fecha_inicio = DateOnly.FromDateTime(DateTime.Today),
                fecha_fin = DateOnly.FromDateTime(DateTime.Today.AddDays(plan.duracion_dias)),
                accesos_restantes = plan.total_accesos,
                estado = "activa"
            });
            await _context.SaveChangesAsync();
            return Ok(new { message = "Suscripción renovada correctamente" });
        }
    }

    public class CrearClienteRequest
    {
        public string nombre { get; set; } = string.Empty;
        public string documento_identidad { get; set; } = string.Empty;
        public string pass { get; set; } = string.Empty;
        public string? email { get; set; }
        public string? telefono { get; set; }
        public int? id_plan { get; set; }
    }

    public class ModificarClienteRequest
    {
        public string? nombre { get; set; }
        public string? email { get; set; }
        public string? telefono { get; set; }
        public string? pass { get; set; }
    }

    public class RenovarRequest { public int id_plan { get; set; } }
}