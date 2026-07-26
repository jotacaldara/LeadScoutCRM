using System.Text;
using System.Text.Json;

namespace LeadScoutCRM.Services;

public class GeminiService : IAiService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiService> _logger;

    private const string BaseUrl =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

    // Nomes fictícios para as assinaturas — evita placeholders como [Nome]
    private static readonly string[] SenderNames =
        ["Joao Caldara"];

    private static readonly Random _rng = new();

    public GeminiService(HttpClient http, IConfiguration config, ILogger<GeminiService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<string> GenerateOutreachMessageAsync(
        string businessName, string? niche, string? city,
        string? phoneNumber, string? website, string messageType)
    {
        var apiKey = _config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API Key não configurada.");

        var senderName = SenderNames[_rng.Next(SenderNames.Length)];

        var prompt = messageType switch
        {
            "whatsapp" => BuildWhatsAppPrompt(businessName, niche, city, website, senderName),
            "email" => BuildEmailPrompt(businessName, niche, city, website, senderName),
            "linkedin" => BuildLinkedInPrompt(businessName, niche, city, website),
            _ => BuildWhatsAppPrompt(businessName, niche, city, website, senderName)
        };

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.85,
                maxOutputTokens = 2048,
                topP = 0.95,
                topK = 40
            }
        };

        var url = $"{BaseUrl}?key={apiKey}";
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var raw = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "Erro ao gerar mensagem.";

        // Remove asteriscos de bold/italic que o Gemini às vezes insere
        return raw
            .Replace("**", "")
            .Replace("##", "")
            .Replace("* ", "")
            .Trim();
    }

    // ─── WhatsApp ────────────────────────────────────────────────────────────────

    private static string BuildWhatsAppPrompt(
        string businessName, string? niche, string? city, string? website, string senderName)
    {
        var hasWebsite = !string.IsNullOrEmpty(website);
        var nicheLabel = niche ?? "negócio local";
        var cityLabel = city ?? "Portugal";

        var digitalAngle = hasWebsite
            ? $"Eles já têm website ({website}). O ângulo deve focar em SEO local — aparecerem nos primeiros resultados quando alguém pesquisa '{nicheLabel} {cityLabel}' no Google — e em captar mais leads qualificados através do site existente."
            : $"Não têm website. O ângulo principal é: cada dia sem presença online são clientes que escolhem a concorrência. Pessoas em {cityLabel} pesquisam '{nicheLabel} perto de mim' todos os dias — e neste momento não os encontram.";

        return $"""
            És Ricardo Silva, consultor de marketing digital especializado em ajudar pequenos negócios locais em Portugal a crescer online.
            O teu estilo de escrita é directo, caloroso e honesto. Nunca soas como vendedor.

            Escreve uma mensagem de WhatsApp COMPLETA e DETALHADA para este potencial cliente.
            O nome do remetente é: {senderName}

            EMPRESA:
            - Nome: {businessName}
            - Nicho: {nicheLabel}
            - Cidade: {cityLabel}
            - Contexto digital: {digitalAngle}

            ESTRUTURA QUE TENS DE SEGUIR — escreve cada ponto, não saltes nenhum:

            Linha 1) Saudação directa com o nome da empresa — informal, como se já os conhecesses.

            Linha 2-3) Apresentação rápida: quem és e porque estás a contactar especificamente eles (menciona o nicho e cidade). Mostra que fizeste pesquisa.

            Linha 4-6) O problema real: descreve em detalhe o que está a acontecer neste nicho online em {cityLabel}. Usa números ou observações concretas (podes inventar dados plausíveis como "mais de 70% das pessoas pesquisam online antes de escolher um {nicheLabel}"). Faz a pessoa sentir urgência sem pressionar.

            Linha 7-9) O que fazes e resultados concretos: conta um caso de sucesso fictício mas realista de um negócio do mesmo nicho ou cidade que ajudaste. Inclui resultados específicos (ex: "em 60 dias, a agenda passou de 40% para 95% de ocupação"). Isto constrói credibilidade.

            Linha 10-11) O que propões agora: algo sem compromisso — uma auditoria gratuita de 15 minutos, ou um relatório sobre a presença online deles. Dá uma razão para responderem hoje.

            Linha 12) Despedida calorosa com o nome do remetente: {senderName}

            REGRAS ABSOLUTAS:
            - Usa no máximo 4 emojis, bem distribuídos (não no início de cada linha)
            - Comprimento total: entre 180 e 250 palavras
            - NÃO uses asteriscos, traços, bullets, nem qualquer formatação markdown
            - NÃO escrevas "[Nome]", "[Empresa]", "[resultado]", "[cidade]" nem QUALQUER texto entre parênteses rectos — substitui sempre por conteúdo real e inventado
            - NÃO uses "espero que esteja bem", "venho por este meio", "caro cliente"
            - O nome do remetente no final é: {senderName}
            - Escreve em português europeu (não brasileiro — usa "tens" não "você tem", "fazes" não "você faz")

            ESCREVE APENAS A MENSAGEM. Sem introdução, sem explicação, sem título.
            """;
    }

    // ─── Email ───────────────────────────────────────────────────────────────────

    private static string BuildEmailPrompt(
        string businessName, string? niche, string? city, string? website, string senderName)
    {
        var hasWebsite = !string.IsNullOrEmpty(website);
        var nicheLabel = niche ?? "negócio local";
        var cityLabel = city ?? "Portugal";
        var firstName = senderName.Split(' ')[0];

        var digitalContext = hasWebsite
            ? $"Têm website ({website}) mas provavelmente não estão a aproveitar todo o potencial de SEO local e captação de leads. O ângulo é optimização, não criação."
            : $"Não têm website. Estão a perder clientes que pesquisam '{nicheLabel} em {cityLabel}' todos os dias. O ângulo é urgência competitiva.";

        return $"""
            És {senderName}, consultor sénior de marketing digital para negócios locais em Portugal, com 6 anos de experiência.
            Escreve um email de outreach COMPLETO, profissional e persuasivo.

            EMPRESA DESTINATÁRIA:
            - Nome: {businessName}
            - Nicho: {nicheLabel}
            - Cidade: {cityLabel}
            - Contexto: {digitalContext}

            ESCREVE O EMAIL COMPLETO com TODAS as seguintes secções, sem omitir nenhuma:

            ASSUNTO: [linha de assunto — directa, específica para {nicheLabel} em {cityLabel}, máx 9 palavras, sem clickbait]

            [linha em branco]

            Olá equipa {businessName},

            [PARÁGRAFO 1 — Introdução com contexto específico: 3-4 frases.
            Apresenta-te como {senderName}. Explica que trabalhas especificamente com negócios de {nicheLabel} em Portugal. Demonstra que estudaste o mercado deles em {cityLabel} — menciona algo concreto sobre o nicho (tendências, comportamento dos consumidores, sazonalidade, etc.). Não sejas genérico.]

            [PARÁGRAFO 2 — O diagnóstico do problema: 4-5 frases.
            Descreve em detalhe o problema que a maioria dos {nicheLabel} em {cityLabel} tem online. Usa dados plausíveis (podes inventar: percentagens de pesquisa online, quantos concorrentes aparecem no Google, etc.). Faz a pessoa visualizar o que está a perder. Este é o parágrafo mais importante — tem de criar urgência sem ser alarmista.]

            [PARÁGRAFO 3 — A tua solução e prova social: 4-5 frases.
            Explica o que fazes, focado em resultados (não em processos técnicos). Inclui um caso de estudo fictício mas muito específico: nome inventado de uma empresa similar, cidade, e resultados concretos com números (ex: "A Clínica Saúde Total, no Porto, viu o tráfego orgânico aumentar 180% em 4 meses e passou a receber 23 pedidos de marcação por semana via website"). Específico = credível.]

            [PARÁGRAFO 4 — A proposta de valor sem compromisso: 3-4 frases.
            Propõe uma auditoria gratuita de 20 minutos ou um relatório personalizado sobre a presença online deles. Explica o que vão receber nessa conversa (3 pontos concretos). Dá duas opções de horário esta semana para criar momentum (ex: "Tens disponibilidade esta quarta ou quinta à tarde?").]

            Um abraço,
            {senderName}
            Consultor de Marketing Digital
            📱 +351 91X XXX XXX
            🌐 www.{firstName.ToLower()}digital.pt

            REGRAS:
            - Tom: profissional mas humano — como um especialista de confiança, não como um vendedor
            - Comprimento do corpo: entre 250 e 320 palavras
            - NÃO uses asteriscos, markdown, bullets com traço
            - NÃO escrevas "[texto entre parênteses rectos]" no output final — substitui tudo por conteúdo real e inventado
            - NÃO uses "venho por este meio", "espero que este email o encontre bem"
            - Escreve em português europeu
            - A assinatura usa exactamente os dados acima — não inventes outros

            ESCREVE APENAS O EMAIL COMPLETO. Sem introdução, sem comentários sobre o email, sem explicações.
            """;
    }

    // ─── LinkedIn ────────────────────────────────────────────────────────────────

    private static string BuildLinkedInPrompt(
        string businessName, string? niche, string? city, string? website)
    {
        var nicheLabel = niche ?? "negócio local";
        var cityLabel = city ?? "Portugal";

        var digitalContext = !string.IsNullOrEmpty(website)
            ? "já têm presença online mas podem melhorar a captação digital"
            : "ainda não têm presença digital estruturada";

        return $"""
            És um especialista em vendas B2B no LinkedIn. O teu estilo é profissional, directo e nunca genérico.
            Escreve DUAS mensagens LinkedIn COMPLETAS para o responsável de uma empresa.

            EMPRESA:
            - Nome: {businessName}
            - Nicho: {nicheLabel}
            - Cidade: {cityLabel}
            - Situação digital: {digitalContext}

            ════════════════════════════════════════
            MENSAGEM 1 — PEDIDO DE CONEXÃO
            ════════════════════════════════════════
            [Escreve aqui a mensagem de pedido de conexão. MÁXIMO 300 CARACTERES.
            Deve: (1) Mostrar que conheces o negócio deles especificamente — menciona o nicho e cidade.
            (2) Dar uma razão concreta para conectar — não "gostaria de ampliar a minha rede".
            (3) Despertar curiosidade com um dado ou observação sobre o mercado deles.
            Exemplo do estilo (adapta, não copies): "Trabalho com clínicas dentárias no Porto a triplicar marcações via Google. Vi o trabalho da vossa clínica — tenho alguns dados sobre o mercado local que podem ser relevantes para vocês."]

            ════════════════════════════════════════
            MENSAGEM 2 — FOLLOW-UP PÓS-CONEXÃO
            ════════════════════════════════════════
            [Escreve aqui a mensagem enviada após aceitarem a conexão. MÁXIMO 500 CARACTERES.
            Estrutura obrigatória:
            - Frase 1: agradecimento breve e directo pela conexão (1 linha)
            - Frase 2-3: observação específica sobre o negócio deles e o mercado — mostra pesquisa real (2 linhas)
            - Frase 4-5: o que tens para oferecer, com resultado concreto e fictício mas plausível (2 linhas)
            - Frase 6: pergunta aberta simples que convida resposta — não é um pedido de reunião, é uma questão genuína (1 linha)]

            REGRAS PARA AMBAS AS MENSAGENS:
            - Sem emojis
            - Tom: profissional, directo, respeitoso — LinkedIn não é WhatsApp
            - NÃO uses "espero que esteja bem", "vi o vosso perfil e fiquei impressionado", "tenho uma proposta interessante"
            - NÃO escrevas "[texto entre parênteses rectos]" no output final — substitui por conteúdo real
            - Respeita os limites de caracteres — conta mentalmente
            - Escreve em português europeu
            - Mantém os separadores ════ exactamente como estão acima

            ESCREVE APENAS AS DUAS MENSAGENS COM OS SEPARADORES. Sem introdução nem comentários.
            """;
    }
}