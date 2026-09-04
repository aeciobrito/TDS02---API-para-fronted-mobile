namespace ControleDeEstudos.DTOs
{
    public record LoginDto(string Email, string Senha);
    public record IniciarSessaoDto(int AlunoId);
    public record FinalizarSessaoDto(int AlunoId);
}
