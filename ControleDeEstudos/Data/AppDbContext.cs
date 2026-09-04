using ControleDeEstudos.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleDeEstudos.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base (options) { }

        public DbSet<Aluno> Alunos { get; set; }
        public DbSet<SessaoEstudos> SessoesEstudos { get;set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Aluno>().HasData
                (
                    new Aluno 
                    { 
                        Id = 1,
                        Nome = "Aluno Teste",
                        Email = "aluno@senac.com",
                        Senha = "123"
                    }
                );
        }
    }
}
