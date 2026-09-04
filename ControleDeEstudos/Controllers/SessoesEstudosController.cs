using ControleDeEstudos.Data;
using ControleDeEstudos.DTOs;
using ControleDeEstudos.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleDeEstudos.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessoesEstudosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SessoesEstudosController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Inicar(IniciarSessaoDto dto)
        {
            // ver se o aluno existe
            var alunoExiste = await _context.Alunos.AnyAsync(a => a.Id == dto.AlunoId);

            if (!alunoExiste)
                return NotFound(new { mensagem = "Aluno não encontrado" });

            // ver se há sessão aberta
            var sessaoAberta = await _context.SessoesEstudos
                .AnyAsync(s => s.AlunoId == dto.AlunoId && s.Fim == null);

            if (sessaoAberta)
                return BadRequest(new { mensagem = "Já há sessão aberta" });

            // criar nova sessão
            var novaSessao = new SessaoEstudos
            {
                AlunoId = dto.AlunoId,
                Inicio = DateTime.Now
            };

            _context.SessoesEstudos.Add(novaSessao);
            await _context.SaveChangesAsync();

            return Ok("Deu certo");
        }
    }
}
