using ControleDeEstudos.Data;
using ControleDeEstudos.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleDeEstudos.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var aluno = await _context.Alunos
                .FirstOrDefaultAsync(a => a.Email == dto.Email && a.Senha == dto.Senha);

            if (aluno == null)
            {
                return Unauthorized(new { sucesso = false, mensagem = "Email ou senha incorretos" });
            }

            return Ok(new
            {
                sucesso = true,
                mensagem = "Login realizado com sucesso",
                alunoId = aluno.Id,
                nome = aluno.Nome
            });
        }
    }
}
