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
            var plan = new Plan
            {
                nombre = dto.nombre,
                precio = dto.precio,
                duracion_dias = dto.duracion_dias,
                total_accesos = dto.total_accesos,
                tipo = dto.tipo,
                isActive = true,
                id_compania = dto.id_compania
            };

            _context.Plan.Add(plan);
            await _context.SaveChangesAsync();

            return Ok(plan);
        }

        // PUT api/plan/5/deactivate
        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> DeactivatePlan(int id)
        {
            var plan = await _context.Plan.FindAsync(id);

            if (plan == null)
                return NotFound();

            plan.isActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan desactivado" });
        }

        // PUT api/plan/5/activate
        [HttpPut("{id}/activate")]
        public async Task<IActionResult> ActivatePlan(int id)
        {
            var plan = await _context.Plan.FindAsync(id);

            if (plan == null)
                return NotFound();

            plan.isActive = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan activado" });
        }

        // DELETE api/plan/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(int id)
        {
            var plan = await _context.Plan.FindAsync(id);

            if (plan == null)
                return NotFound();

            if (plan.isActive)
                return BadRequest(new { message = "Solo se pueden borrar planes inactivos" });

            bool tieneUsuarios = await _context.Suscripcion
                .AnyAsync(s => s.id_plan == id);

            if (tieneUsuarios)
                return BadRequest(new
                {
                    message = "No se puede borrar: existen usuarios o histórico con este plan"
                });

            _context.Plan.Remove(plan);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Plan eliminado" });
        }
    }

    public class CrearPlanDto
    {
        public string nombre { get; set; }
        public decimal precio { get; set; }
        public int duracion_dias { get; set; }
        public int total_accesos { get; set; }
        public string tipo { get; set; }
        public int id_compania { get; set; }
    }
}