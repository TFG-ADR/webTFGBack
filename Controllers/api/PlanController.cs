using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using webTFGBack.data;
using webTFGBack.Models;

namespace webTFGBack.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlanController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PlanController(AppDbContext context)
        {
            _context = context;
        }

        // GET api/plan
        [HttpGet]
        public async Task<IActionResult> GetPlanes()
        {
            var planes = await _context.Plan
                .Select(p => new
                {
                    id = p.id_plan,
                    nombre = p.nombre,
                    precio = p.precio,
                    tipo = p.tipo,
                    isActive = p.isActive,
                    miembros = _context.Suscripcion
                        .Count(s => s.id_plan == p.id_plan && s.estado == "activa")
                })
                .ToListAsync();

            int maxMiembros = planes.Any() ? planes.Max(p => p.miembros) : 1;

            var resultado = planes.Select(p => new
            {
                p.id,
                p.nombre,
                p.precio,
                p.tipo,
                p.isActive,
                p.miembros,
                pct = maxMiembros > 0
                    ? Math.Round((double)p.miembros / maxMiembros * 100, 1)
                    : 0
            });

            return Ok(resultado);
        }

        // POST api/plan
        [HttpPost]
        public async Task<IActionResult> CrearPlan([FromBody] CrearPlanDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.nombre))
                return BadRequest(new { message = "El nombre es obligatorio" });
            if (dto.precio <= 0)
                return BadRequest(new { message = "El precio debe ser mayor que 0" });
            if (dto.duracion_dias <= 0)
                return BadRequest(new { message = "La duración debe ser mayor que 0" });

            // Obtener id_compania del gym si no se manda
            int idCompania = dto.id_compania;
            if (idCompania == 0)
            {
                var gym = await _context.Gym.FirstOrDefaultAsync();
                if (gym == null)
                    return BadRequest(new { message = "No hay ningún gimnasio configurado" });
                idCompania = gym.id_compania;
            }

            var plan = new Plan
            {
                nombre = dto.nombre.Trim(),
                precio = dto.precio,
                duracion_dias = dto.duracion_dias,
                total_accesos = dto.total_accesos,
                tipo = dto.tipo?.Trim() ?? "",
                isActive = true,
                id_compania = idCompania
            };

            _context.Plan.Add(plan);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan creado correctamente", id_plan = plan.id_plan });
        }

        // PUT api/plan/{id}/deactivate
        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> DeactivatePlan(int id)
        {
            var plan = await _context.Plan.FindAsync(id);
            if (plan == null)
                return NotFound(new { message = "Plan no encontrado" });

            plan.isActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan desactivado correctamente" });
        }

        // PUT api/plan/{id}/activate
        [HttpPut("{id}/activate")]
        public async Task<IActionResult> ActivatePlan(int id)
        {
            var plan = await _context.Plan.FindAsync(id);
            if (plan == null)
                return NotFound(new { message = "Plan no encontrado" });

            plan.isActive = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan activado correctamente" });
        }

        // DELETE api/plan/{id}
        // Solo se puede borrar si está inactivo Y no tiene suscripciones activas
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var plan = await _context.Plan.FindAsync(id);
            if (plan == null)
                return NotFound(new { message = "Plan no encontrado" });

            if (plan.isActive)
                return BadRequest(new { message = "Solo se pueden borrar planes inactivos. Desactívalo primero." });

            // Bloqueamos solo si tiene suscripciones ACTIVAS (no el historial)
            bool tieneActivos = await _context.Suscripcion
                .AnyAsync(s => s.id_plan == id && s.estado == "activa");

            if (tieneActivos)
                return BadRequest(new { message = "No se puede borrar: el plan tiene socios activos. Espera a que venzan sus suscripciones." });

            _context.Plan.Remove(plan);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan eliminado correctamente" });
        }
    }

    public class CrearPlanDto
    {
        public string nombre { get; set; } = string.Empty;
        public decimal precio { get; set; }
        public int duracion_dias { get; set; }
        public int total_accesos { get; set; }
        public string tipo { get; set; } = string.Empty;
        public int id_compania { get; set; }
    }
}