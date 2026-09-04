# 📘 Tutorial de Implementação: Integração API .NET 8 + React Native

Este tutorial descreve **passo a passo**, em ordem cronológica de desenvolvimento, todas as modificações de código necessárias para construir a API backend e integrá-la com o aplicativo frontend React Native (Expo).

---

## 📑 Sumário
1. [Etapa 1: Preparação e Configurações de Rede da API](#etapa-1-preparação-e-configurações-de-rede-da-api)
2. [Etapa 2: DTOs e Regras de Negócio na API](#etapa-2-dtos-e-regras-de-negócio-na-api)
3. [Etapa 3: Como Executar e Testar a API no Swagger](#etapa-3-como-executar-e-testar-a-api-no-swagger)
4. [Etapa 4: Frontend - Camada de Serviços HTTP (`api.js`)](#etapa-4-frontend---camada-de-serviços-http-apijs)
5. [Etapa 5: Frontend - Integração da Tela de Login](#etapa-5-frontend---integração-da-tela-de-login)
6. [Etapa 6: Frontend - Integração da Tela Home (Sessões e Horas)](#etapa-6-frontend---integração-da-tela-home-sessões-e-horas)
7. [Etapa 7: Roteiro Completo de Testes de Ponta a Ponta](#etapa-7-roteiro-completo-de-testes-de-ponta-a-ponta)

---

## Etapa 1: Preparação e Configurações de Rede da API

Para que a API receba conexões do frontend (seja na Web, Emulador Android ou Celular via Expo Go), precisamos ajustar permissões de host, política de CORS e escuta de rede.

### 1.1. Liberar Hosts no `appsettings.json` e `appsettings.Development.json`
Garantir que a chave `AllowedHosts` permita qualquer origem:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 1.2. Habilitar CORS e Ajustar Redirecionamento no `Program.cs`
No arquivo `ControleDeEstudos/Program.cs`:
1. Registre o serviço de CORS com política permissiva (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`).
2. Adicione `app.UseCors()` antes de `app.UseAuthorization()`.
3. Desative ou comente `app.UseHttpsRedirection()` para que requisições HTTP locais do emulador/celular não quebrem por falta de certificado SSL.

```csharp
using ControleDeEstudos.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=estudos.db"));

// 1. Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 2. Aplica o CORS
app.UseCors();

// 3. Comentar redirecionamento HTTPS em desenvolvimento local:
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();
```

### 1.3. Escuta em Todas as Interfaces de Rede (`Properties/launchSettings.json`)
Altere a `applicationUrl` nos perfis `http` e `https` para incluir `http://0.0.0.0:5036`:

```json
"profiles": {
  "http": {
    "commandName": "Project",
    "dotnetRunMessages": true,
    "launchBrowser": true,
    "launchUrl": "swagger",
    "applicationUrl": "http://0.0.0.0:5036",
    "environmentVariables": {
      "ASPNETCORE_ENVIRONMENT": "Development"
    }
  }
}
```

---

## Etapa 2: DTOs e Regras de Negócio na API

### 2.1. Definir os DTOs em `DTOs/AppDtos.cs`
Crie ou atualize os DTOs que representam as entradas das requisições:

```csharp
namespace ControleDeEstudos.DTOs
{
    public record LoginDto(string Email, string Senha);
    public record IniciarSessaoDto(int AlunoId);
    public record FinalizarSessaoDto(int AlunoId);
    public record AtualizarSessaoDto(DateTime Inicio, DateTime? Fim);
}
```

### 2.2. Ajustar o `AuthController.cs`
No endpoint de login, garanta que respostas de erro também retornem um JSON estruturado para facilitar o consumo no frontend:

```csharp
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
```

### 2.3. Implementar o `SessoesEstudosController.cs`
Substitua o arquivo por completo com os métodos de controle de sessão e estatísticas:

```csharp
using ControleDeEstudos.Data;
using ControleDeEstudos.DTOs;
using ControleDeEstudos.Models;
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

        // POST: api/SessoesEstudos/iniciar
        [HttpPost]
        [HttpPost("iniciar")]
        public async Task<IActionResult> Iniciar([FromBody] IniciarSessaoDto dto)
        {
            var alunoExiste = await _context.Alunos.AnyAsync(a => a.Id == dto.AlunoId);
            if (!alunoExiste)
                return NotFound(new { mensagem = "Aluno não encontrado" });

            var sessaoAberta = await _context.SessoesEstudos
                .AnyAsync(s => s.AlunoId == dto.AlunoId && s.Fim == null);

            if (sessaoAberta)
                return BadRequest(new { mensagem = "Já existe uma sessão de estudos em andamento para este aluno." });

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
    }
}
```

---

## Etapa 3: Como Executar e Testar a API no Swagger

1. No terminal, dentro da pasta `ControleDeEstudos`, execute:
   ```bash
   dotnet run
   ```
2. Abra no navegador: `http://localhost:5036/swagger`
3. **Teste 1 - Login:**
   - Execute o endpoint `POST /api/Auth/login` com:
     ```json
     { "email": "aluno@senac.com", "senha": "123" }
     ```
   - Verifique se o retorno é `200 OK` com `alunoId: 1`.
4. **Teste 2 - Iniciar Sessão:**
   - Execute `POST /api/SessoesEstudos/iniciar` com:
     ```json
     { "alunoId": 1 }
     ```
   - Verifique o retorno de sessão criada. Tente executar novamente em seguida e confirme se retorna `400 BadRequest` avisando que já existe sessão aberta.
5. **Teste 3 - Finalizar Sessão:**
   - Execute `POST /api/SessoesEstudos/finalizar` com `{ "alunoId": 1 }`.
   - Verifique o retorno com o cálculo da duração da sessão.

---

## Etapa 4: Frontend - Camada de Serviços HTTP (`api.js`)

Crie o arquivo `src/services/api.js` no projeto React Native. Ele isola as chamadas nativas de `fetch`:

```javascript
// src/services/api.js

export const API_BASE_URL = 'http://localhost:5036/api';

// 1. Realiza login e retorna dados do aluno
export async function apiLogin(email, senha) {
    const resposta = await fetch(`${API_BASE_URL}/Auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, senha }),
    });

    const dados = await resposta.json();
    if (!resposta.ok) {
        throw new Error(dados.mensagem || 'Falha ao autenticar.');
    }
    return dados;
}

// 2. Inicia uma sessão para o aluno
export async function apiIniciarSessao(alunoId) {
    const resposta = await fetch(`${API_BASE_URL}/SessoesEstudos/iniciar`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ alunoId }),
    });

    const dados = await resposta.json();
    if (!resposta.ok) {
        throw new Error(dados.mensagem || 'Falha ao iniciar sessão.');
    }
    return dados;
}

// 3. Finaliza a sessão ativa e retorna a duração
export async function apiFinalizarSessao(alunoId) {
    const resposta = await fetch(`${API_BASE_URL}/SessoesEstudos/finalizar`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ alunoId }),
    });

    const dados = await resposta.json();
    if (!resposta.ok) {
        throw new Error(dados.mensagem || 'Falha ao finalizar sessão.');
    }
    return dados;
}

// 4. Verifica se o aluno já tem estudo em andamento
export async function apiObterSessaoAtiva(alunoId) {
    try {
        const resposta = await fetch(`${API_BASE_URL}/SessoesEstudos/ativa/${alunoId}`);
        if (!resposta.ok) return { emAndamento: false };
        return await resposta.json();
    } catch {
        return { emAndamento: false };
    }
}

// 5. Retorna o total de horas e de sessões concluídas
export async function apiObterTotalHoras(alunoId) {
    try {
        const resposta = await fetch(`${API_BASE_URL}/SessoesEstudos/total-horas/aluno/${alunoId}`);
        if (!resposta.ok) return null;
        return await resposta.json();
    } catch {
        return null;
    }
}
```

---

## Etapa 5: Frontend - Integração da Tela de Login

No arquivo `src/screens/LoginScreen.js`:
1. Importe `apiLogin`.
2. Adicione estado de `loading`.
3. Chame a API dentro de `handleLogin`.
4. Ao ter sucesso, use `navigation.replace('Home', { studentId: resultado.alunoId, studentName: resultado.nome })`.

```javascript
import { useState } from "react";
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from "react-native";
import InputField from "../components/InputField";
import PrimaryButton from "../components/PrimaryButton";
import { colors } from "../styles/colors";
// [NOVO] Importação do serviço de login da API
import { apiLogin } from "../services/api";

export default function LoginScreen({ navigation }) {
    const [email, setEmail] = useState('aluno@senac.com');
    const [password, setPassword] = useState('123');
    const [errorMessage, setErrorMessage] = useState('');
    // [NOVO] Estado para desabilitar o botão e exibir feedback de carregamento
    const [loading, setLoading] = useState(false);

    async function handleLogin() {
        const normalizedEmail = email.trim().toLowerCase();

        if (!normalizedEmail || !password) {
            setErrorMessage('Preencha o email e a senha.');
            return;
        }

        setErrorMessage('');
        setLoading(true);

        try {
            // [ALTERADO] Substituída verificação estática por chamada real à API
            const resultado = await apiLogin(normalizedEmail, password);

            // [ALTERADO] Redirecionamento enviando o studentId e studentName recebidos do backend
            navigation.replace('Home', {
                studentId: resultado.alunoId,
                studentName: resultado.nome,
            });
        } catch (erro) {
            // [NOVO] Captura da mensagem de erro retornada pela API
            setErrorMessage(erro.message || 'Não foi possível conectar à API.');
        } finally {
            setLoading(false);
        }
    }

    return (
        <KeyboardAvoidingView
            style={styles.keyboardArea}
            behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
            <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
                <View style={styles.header}>
                    <Text style={styles.logo}>SF</Text>
                    <Text style={styles.title}>Bem-Vindo ao StudyFlow</Text>
                    <Text style={styles.subtitle}>Entre para acompanhar sua rotina de estudos</Text>
                </View>

                <View style={styles.form}>
                    <InputField
                        label="Email"
                        value={email}
                        onChangeText={setEmail}
                        placeholder="Digite seu email"
                        keyboardType="email-address"
                        autoCapitalize="none"
                    />

                    <InputField
                        label="Senha"
                        value={password}
                        onChangeText={setPassword}
                        placeholder="Digite sua senha"
                        autoCapitalize="none"
                        secureTextEntry={true}
                    />

                    {errorMessage ? <Text style={styles.errorText}>{errorMessage}</Text> : null}

                    {/* [ALTERADO] Botão bloqueado durante o carregamento com texto dinâmico */}
                    <PrimaryButton
                        title={loading ? "Entrando..." : "Entrar"}
                        onPress={handleLogin}
                        disabled={loading}
                    />
                </View>
            </ScrollView>
        </KeyboardAvoidingView>
    );
}

const styles = StyleSheet.create({
    keyboardArea: { flex: 1, backgroundColor: colors.background },
    container: { flexGrow: 1, justifyContent: 'center', padding: 24, gap: 36 },
    header: { alignItems: 'center', gap: 1 },
    logo: { color: colors.primary, fontSize: 58, fontWeight: '800' },
    title: { maxWidth: 320, color: colors.textLight, fontSize: 15, lineHeight: 22, textAlign: 'center' },
    form: { gap: 18 },
    errorText: { color: colors.erro, fontSize: 14, fontWeight: '600', textAlign: 'center' }
});
```

---

## Etapa 6: Frontend - Integração da Tela Home (Sessões e Horas)

No arquivo `src/screens/HomeScreen.js`:
1. Recupere `studentId` dos parâmetros de rota: `route.params?.studentId ?? 1`.
2. Use `useEffect` para carregar o estado inicial (se já há sessão ativa e quantas horas foram acumuladas).
3. Na função do botão (`handleToggleSession`), alterne entre `apiIniciarSessao` e `apiFinalizarSessao`.

```javascript
// [ALTERADO] Adicionados useEffect e useCallback
import { useState, useEffect, useCallback } from "react";
import { ScrollView, StyleSheet, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { colors } from "../styles/colors";
import InfoCard from "../components/Infocard";
import PrimaryButton from "../components/PrimaryButton";
// [NOVO] Importação dos serviços de sessão e horas da API
import {
    apiIniciarSessao,
    apiFinalizarSessao,
    apiObterSessaoAtiva,
    apiObterTotalHoras
} from "../services/api";

export default function HomeScreen({ route, navigation }) {
    // [ALTERADO] Leitura do studentId recebido da tela de Login
    const studentId = route.params?.studentId ?? 1;
    const studentName = route.params?.studentName ?? 'Estudante';

    const [sessionStarted, setSessionStarted] = useState(false);
    // [NOVO] Estados para armazenar totais, mensagem de feedback e loading
    const [totalSessoes, setTotalSessoes] = useState(0);
    const [tempoEstudado, setTempoEstudado] = useState('0h 0m');
    const [feedbackMessage, setFeedbackMessage] = useState('');
    const [loading, setLoading] = useState(false);

    // [NOVO] Função para buscar o resumo de horas do aluno na API
    const carregarTotais = useCallback(async () => {
        const dados = await apiObterTotalHoras(studentId);
        if (dados) {
            setTotalSessoes(dados.totalSessoesConcluidas);
            setTempoEstudado(dados.tempoFormatado || `${dados.horasDecimais}h`);
        }
    }, [studentId]);

    // [NOVO] Executado ao abrir a tela: checa sessão ativa no banco e carrega totais
    useEffect(() => {
        async function carregarDadosIniciais() {
            const ativa = await apiObterSessaoAtiva(studentId);
            if (ativa && ativa.emAndamento) {
                setSessionStarted(true);
            }
            await carregarTotais();
        }
        carregarDadosIniciais();
    }, [studentId, carregarTotais]);

    // [ALTERADO] Função que alterna entre iniciar e finalizar sessão na API
    async function handleToggleSession() {
        setLoading(true);
        setFeedbackMessage('');

        try {
            if (!sessionStarted) {
                // [NOVO] Chamada para iniciar sessão no backend
                const res = await apiIniciarSessao(studentId);
                setSessionStarted(true);
                setFeedbackMessage(res.mensagem || 'Sessão iniciada com sucesso!');
            } else {
                // [NOVO] Chamada para finalizar sessão no backend
                const res = await apiFinalizarSessao(studentId);
                setSessionStarted(false);
                setFeedbackMessage(`Sessão finalizada! Duração: ${res.duracao}`);
                // [NOVO] Recarrega os totais na tela após finalizar
                await carregarTotais();
            }
        } catch (erro) {
            setFeedbackMessage(erro.message || 'Erro ao processar sessão.');
        } finally {
            setLoading(false);
        }
    }

    return (
        <SafeAreaView style={styles.safeArea}>
            <ScrollView contentContainerStyle={styles.container}>
                <View style={styles.header}>
                    <View>
                        <Text style={styles.greeting}>Olá, {studentName}</Text>
                        <Text style={styles.subtitle}>ID do Aluno: #{studentId}</Text>
                    </View>
                    <Text style={styles.logoutText} onPress={() => navigation.replace('Login')}>Sair</Text>
                </View>

                {/* [ALTERADO] Cartões exibindo dados dinâmicos da API */}
                <View style={styles.cardRow}>
                    <InfoCard icon="✓" value={totalSessoes.toString()} title="Sessões Concluídas" />
                    <InfoCard icon="⏱" value={tempoEstudado} title="Tempo Estudado" />
                </View>

                {/* Status da Sessão */}
                <View style={styles.mensageCard}>
                    <Text style={styles.mensageTitle}>
                        {sessionStarted ? 'Sessão em andamento' : 'Nenhuma sessão ativa'}
                    </Text>
                    <Text style={styles.mensageText}>
                        {sessionStarted
                            ? 'O cronômetro está rodando no servidor. Bons estudos!'
                            : 'Toque no botão abaixo para começar a contabilizar seu tempo.'}
                    </Text>
                    {/* [NOVO] Exibição de mensagem de retorno da API */}
                    {feedbackMessage ? <Text style={styles.feedbackText}>{feedbackMessage}</Text> : null}
                </View>

                {/* [ALTERADO] Botão com loading e controle dinâmico de estado */}
                <PrimaryButton
                    title={loading ? 'Aguarde...' : (sessionStarted ? 'Finalizar Sessão' : 'Iniciar Sessão')}
                    onPress={handleToggleSession}
                    disabled={loading}
                />
            </ScrollView>
        </SafeAreaView>
    );
}

const styles = StyleSheet.create({
    safeArea: { flex: 1, backgroundColor: colors.background },
    container: { flexGrow: 1, padding: 24, gap: 22 },
    header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
    greeting: { color: colors.text, fontSize: 26, fontWeight: '800' },
    subtitle: { color: colors.textLight, fontSize: 14, marginTop: 2 },
    logoutText: { color: colors.primary, fontSize: 15, fontWeight: '700', paddingVertical: 6 },
    cardRow: { flexDirection: 'row', gap: 14 },
    mensageCard: { gap: 8, borderRadius: 18, backgroundColor: colors.surface, padding: 20, borderWidth: 1, borderColor: colors.border },
    mensageTitle: { color: colors.text, fontWeight: '800', fontSize: 18, lineHeight: 24 },
    mensageText: { color: colors.textLight, fontSize: 14, lineHeight: 20 },
    feedbackText: { marginTop: 8, color: colors.primaryDark, fontSize: 14, fontWeight: '700' }
});
```

---

## Etapa 7: Roteiro Completo de Testes de Ponta a Ponta

1. **Iniciar a API:**
   ```bash
   dotnet run
   ```
2. **Iniciar o Frontend:**
   ```bash
   npx expo start
   ```
3. **Fluxo de Teste no App:**
   - Na tela de login, clique em **Entrar** com `aluno@senac.com` e `123`.
   - Observe a navegação para a Home, exibindo `Olá, Aluno Teste` e `ID: #1`.
   - Clique em **Iniciar Sessão**: o card muda para "Sessão em andamento" e o botão passa a ser "Finalizar Sessão".
   - Aguarde alguns segundos e clique em **Finalizar Sessão**: o botão volta para "Iniciar Sessão", o feedback exibe a duração exata e o cartão de "Sessões Concluídas" e "Tempo Estudado" atualiza imediatamente com os dados recalculados no banco SQLite!
