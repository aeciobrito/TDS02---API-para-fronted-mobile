using System.Text.Json.Serialization;

namespace ControleDeEstudos.Models
{
    public class SessaoEstudos
    {
        public int Id { get; set; }
        public int AlunoId { get; set; }
        public DateTime Inicio { get; set; }
        public DateTime? Fim {  get; set; }

        [JsonIgnore]
        public Aluno? Aluno { get; set; }
    }
}
