using System.Text.Json.Serialization;

namespace ControleDeEstudos.Models
{
    public class Aluno
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;

        [JsonIgnore]
        public List<SessaoEstudos> Sessoes { get; set; } = new();
    }
}
