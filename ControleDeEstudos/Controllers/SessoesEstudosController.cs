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

        // POST: api/SessoesEstudos ou api/SessoesEstudos/iniciar
        [HttpPost]
        [HttpPost("iniciar")]
        public async Task<IActionResult> Iniciar([FromBody] IniciarSessaoDto dto)
        {
            // 1. Verifica se o aluno existe no banco
            var alunoExiste = await _context.Alunos.AnyAsync(a => a.Id == dto.AlunoId);
            if (!alunoExiste)
                return NotFound(new { mensagem = "Aluno não encontrado" });

            // 2. Verifica se já existe uma sessão aberta (sem Fim registrado)
            var sessaoAberta = await _context.SessoesEstudos
                .AnyAsync(s => s.AlunoId == dto.AlunoId && s.Fim == null);

            if (sessaoAberta)
                return BadRequest(new { mensagem = "Já existe uma sessão de estudos em andamento para este aluno." });

            // 3. Cria a nova sessão com a data e hora atual
            var novaSessao = new SessaoEstudos
            {
                AlunoId = dto.AlunoId,
                Inicio = DateTime.Now
            };

            _context.SessoesEstudos.Add(novaSessao);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensagem = "Sessão iniciada com sucesso!",
                sessao = novaSessao
            });
        }

        // POST: api/SessoesEstudos/finalizar
        [HttpPost("finalizar")]
        public async Task<IActionResult> Finalizar([FromBody] FinalizarSessaoDto dto)
        {
            // Busca a sessão em andamento mais recente (Fim == null)
            var sessao = await _context.SessoesEstudos
                .OrderByDescending(s => s.Inicio)
                .FirstOrDefaultAsync(s => s.AlunoId == dto.AlunoId && s.Fim == null);

            if (sessao == null)
                return NotFound(new { mensagem = "Nenhuma sessão em andamento encontrada." });

            sessao.Fim = DateTime.Now;
            await _context.SaveChangesAsync();

            var tempoDecorrido = sessao.Fim.Value - sessao.Inicio;

            return Ok(new
            {
                mensagem = "Sessão finalizada com sucesso!",
                sessao,
                duracao = $"{tempoDecorrido.Hours}h {tempoDecorrido.Minutes}m {tempoDecorrido.Seconds}s"
            });
        }

        // GET: api/SessoesEstudos/ativa/1
        // Permite ao app mobile saber se o aluno já está com uma sessão aberta
        [HttpGet("ativa/{alunoId}")]
        public async Task<IActionResult> ObterSessaoAtiva(int alunoId)
        {
            var sessao = await _context.SessoesEstudos
                .OrderByDescending(s => s.Inicio)
                .FirstOrDefaultAsync(s => s.AlunoId == alunoId && s.Fim == null);

            if (sessao == null)
                return Ok(new { emAndamento = false, sessao = (SessaoEstudos?)null });

            return Ok(new { emAndamento = true, sessao });
        }

        // GET: api/SessoesEstudos/aluno/1
        [HttpGet("aluno/{alunoId}")]
        public async Task<IActionResult> ListarPorAluno(int alunoId)
        {
            var sessoes = await _context.SessoesEstudos
                .Where(s => s.AlunoId == alunoId)
                .OrderByDescending(s => s.Inicio)
                .ToListAsync();

            return Ok(sessoes);
        }

        // GET: api/SessoesEstudos/total-horas/aluno/1
        [HttpGet("total-horas/aluno/{alunoId}")]
        public async Task<IActionResult> ObterTotalHoras(int alunoId)
        {
            var sessoesConcluidas = await _context.SessoesEstudos
                .Where(s => s.AlunoId == alunoId && s.Fim != null)
                .ToListAsync();

            var totalSegundos = sessoesConcluidas
                .Sum(s => (s.Fim!.Value - s.Inicio).TotalSeconds);

            var tempoTotal = TimeSpan.FromSeconds(totalSegundos);

            return Ok(new
            {
                alunoId,
                totalSessoesConcluidas = sessoesConcluidas.Count,
                horasDecimais = Math.Round(tempoTotal.TotalHours, 2),
                tempoFormatado = $"{(int)tempoTotal.TotalHours}h {tempoTotal.Minutes}m {tempoTotal.Seconds}s"
            });
        }

        // GET: api/SessoesEstudos/5
        [HttpGet("{id}")]
        public async Task<IActionResult> ObterPorId(int id)
        {
            var sessao = await _context.SessoesEstudos.FindAsync(id);
            if (sessao == null)
                return NotFound(new { mensagem = "Sessão não encontrada." });

            return Ok(sessao);
        }

        // PUT: api/SessoesEstudos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Atualizar(int id, [FromBody] AtualizarSessaoDto dto)
        {
            var sessao = await _context.SessoesEstudos.FindAsync(id);
            if (sessao == null)
                return NotFound(new { mensagem = "Sessão não encontrada." });

            sessao.Inicio = dto.Inicio;
            sessao.Fim = dto.Fim;

            await _context.SaveChangesAsync();
            return Ok(new { mensagem = "Sessão atualizada com sucesso.", sessao });
        }

        // DELETE: api/SessoesEstudos/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Remover(int id)
        {
            var sessao = await _context.SessoesEstudos.FindAsync(id);
            if (sessao == null)
                return NotFound(new { mensagem = "Sessão não encontrada." });

            _context.SessoesEstudos.Remove(sessao);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
