# ⚡ LeadScout CRM

Plataforma SaaS multi-tenant de prospeção e gestão de leads comerciais, pensada para freelancers e agências de marketing digital que trabalham com negócios locais.

Este projeto foi desenvolvido no âmbito da **Prova de Aptidão Profissional (PAP)** do curso de Técnico de Gestão e Programação de Sistemas Informáticos (TGPSI), no INETE. A motivação surge da experiência real do autor em marketing digital freelance, nomeadamente na prospeção telefónica de negócios locais — um processo que esta aplicação visa tornar mais eficiente e organizado.

---

## 📖 Índice

- [Visão geral](#-visão-geral)
- [Funcionalidades](#-funcionalidades)
- [Stack tecnológica](#-stack-tecnológica)
- [Arquitetura](#-arquitetura)
- [Estrutura do projeto](#-estrutura-do-projeto)
- [Pré-requisitos](#-pré-requisitos)
- [Instalação e configuração](#-instalação-e-configuração)
- [Configuração de segredos (User Secrets)](#-configuração-de-segredos-user-secrets)
- [Base de dados e migrations](#-base-de-dados-e-migrations)
- [Stripe — testar pagamentos localmente](#-stripe--testar-pagamentos-localmente)
- [Correr a aplicação](#-correr-a-aplicação)
- [Planos e limites](#-planos-e-limites)
- [Segurança](#-segurança)
- [Notas de arquitetura](#-notas-de-arquitetura)
- [Autor](#-autor)
- [Licença](#-licença)

---

## 🔍 Visão geral

O LeadScout CRM permite que um profissional de marketing digital:

1. Pesquise negócios locais por nicho e localização, através da **Google Places API**.
2. Guarde esses negócios como *leads* no seu CRM pessoal (dados isolados por utilizador — multi-tenant).
3. Organize o processo comercial num **pipeline Kanban** (Novo → Mensagem Enviada → Em Negociação → Cliente Fechado / Rejeitado).
4. Gere mensagens de outreach personalizadas (WhatsApp, Email, LinkedIn) através de **IA generativa** (Google Gemini 2.5 Flash).
5. Acompanhe o desempenho através de um painel de **Analytics** (taxa de conversão, nichos mais rentáveis, atividade diária).
6. Faça upgrade do seu plano de subscrição através de **Stripe Checkout**, com ativação automática via *webhooks*.

Existe ainda uma área de **Administração** completa, com métricas de negócio (MRR, ARR, ARPU), gestão de utilizadores, exportação de dados e envio de emails segmentados.

---

## ✨ Funcionalidades

### Para o utilizador (freelancer / agência)
- Registo e autenticação segura (ASP.NET Core Identity)
- Pesquisa de negócios locais via Google Places, com enriquecimento automático de dados (telefone, website)
- Gestão de leads com filtros por estado, nicho e pesquisa de texto
- Pipeline Kanban com *drag-and-drop* nativo (HTML5) e atualização otimista da interface
- Sistema de notas por lead
- Pontuação automática de leads (*lead scoring*) com base em dados disponíveis e nível de interação
- Geração de mensagens de outreach com IA, adaptadas ao nicho, cidade e canal de contacto
- Painel de Analytics com gráficos (Chart.js)
- Gestão de subscrição via portal Stripe (upgrade, cancelamento, faturação)

### Para o administrador
- Dashboard com métricas de negócio em tempo real (MRR, ARR, conversão)
- Gestão de utilizadores (alterar plano manualmente, eliminar conta)
- Alertas automáticos de utilizadores perto do limite do plano Free ou com pagamentos em falta
- Centro de email para campanhas segmentadas por plano/estado
- Exportação de utilizadores em CSV

---

## 🛠️ Stack tecnológica

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core MVC 8.0 (C#) |
| Base de dados | SQLite + Entity Framework Core 8 |
| Autenticação | ASP.NET Core Identity (cookies, PBKDF2, *lockout* de força bruta) |
| Pagamentos | Stripe (Checkout, Billing Portal, Webhooks) |
| Dados de negócios | Google Places API |
| Inteligência Artificial | Google Gemini 2.5 Flash |
| Frontend | Razor Views + Bootstrap 5 + Bootstrap Icons |
| Gráficos | Chart.js |
| Documentação de API | Swagger / Swashbuckle |

> **Nota sobre a base de dados:** o projeto usa SQLite por simplicidade de desenvolvimento e portabilidade, mas a arquitetura assenta inteiramente sobre o Entity Framework Core, que abstrai o fornecedor de dados. O pacote `Microsoft.EntityFrameworkCore.SqlServer` já está referenciado no `.csproj` — a migração para SQL Server exigiria apenas alterar a linha de configuração em `Program.cs` (de `UseSqlite` para `UseSqlServer`), sem qualquer alteração ao modelo de dados ou à lógica de negócio.

---

## 🏗️ Arquitetura

A aplicação segue o padrão **MVC** (Model-View-Controller) com uma separação clara de responsabilidades:

```
Controllers/        → Lógica de orquestração HTTP (MVC + API REST)
Models/Entities/     → Entidades de domínio mapeadas pelo EF Core
Models/ViewModels/   → DTOs de entrada e saída (RequestModels, ViewModels)
Services/            → Lógica de negócio isolada (Stripe, Google Places, Gemini, Email)
Data/                → AppDbContext, seeding inicial
Migrations/          → Histórico de alterações ao esquema da base de dados
Views/               → Interface Razor (server-side rendering)
Filters/             → Filtros de ação personalizados (ex: validação de plano de subscrição)
```

### Multi-tenancy

Todas as entidades de negócio (`Lead`, `Note`) estão associadas a um `UserId`, garantindo isolamento total entre contas. Existe um índice único composto em `(GooglePlaceId, UserId)`, que impede duplicados do mesmo negócio *para o mesmo utilizador*, mas permite que utilizadores diferentes guardem o mesmo negócio de forma independente.

### Controlo de planos

O atributo personalizado `[SubscriptionRequired(SubscriptionPlan.Pro)]`, implementado como `IAsyncActionFilter`, intercepta pedidos a *endpoints* que exigem um plano mínimo, devolvendo o código HTTP `402 Payment Required` quando aplicável.

---

## 📁 Estrutura do projeto

```
LeadScoutCRM/
├── Controllers/
│   ├── AccountController.cs
│   ├── AdminController.cs
│   ├── AnalyticsController.cs
│   ├── HomeController.cs
│   ├── LeadsController.cs
│   ├── Api/LeadsApiController.cs
│   ├── PricingController.cs
│   └── StripeWebhookController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── DbSeeder.cs
├── Filters/
│   └── SubscriptionRequiredAttribute.cs
├── Migrations/
├── Models/
│   ├── Entities/         (Lead, Note, ApplicationUser, SubscriptionPlan, LeadStatus)
│   └── ViewModels/        (RequestModels, ViewModels, AccountViewModels)
├── Services/
│   ├── GooglePlacesService.cs
│   ├── GeminiService.cs
│   ├── SubscriptionService.cs
│   ├── EmailService.cs
│   └── LeadScoringService.cs
├── Views/
├── appsettings.json        (sem segredos — ver secção abaixo)
└── Program.cs
```

---

## ✅ Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQLite](https://www.sqlite.org/) (não é necessária instalação separada — o driver EF Core trata da criação do ficheiro `.db`)
- Uma conta [Stripe](https://dashboard.stripe.com/register) em modo de teste
- Uma chave de API do [Google Places](https://console.cloud.google.com/)
- Uma chave de API do [Google AI Studio (Gemini)](https://aistudio.google.com/)
- [Stripe CLI](https://stripe.com/docs/stripe-cli) (para testar *webhooks* localmente)
- Visual Studio 2022 / VS Code (opcional, mas recomendado)

---

## ⚙️ Instalação e configuração

Clonar o repositório e restaurar as dependências:

```bash
git clone https://github.com/<o-teu-utilizador>/LeadScoutCRM.git
cd LeadScoutCRM
dotnet restore
```

> ⚠️ O ficheiro `appsettings.json` neste repositório **não contém quaisquer chaves ou segredos reais** — apenas a estrutura de configuração. Todos os valores sensíveis devem ser fornecidos através de **User Secrets** (desenvolvimento) ou **variáveis de ambiente** (produção), conforme descrito abaixo.

---

## 🔐 Configuração de segredos (User Secrets)

O projeto já está preparado com um `UserSecretsId` no `.csproj`. Para configurar o ambiente de desenvolvimento local:

```bash
dotnet user-secrets set "Stripe:SecretKey" "sk_test_XXXXXXXXXXXX"
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_XXXXXXXXXXXX"
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_XXXXXXXXXXXX"
dotnet user-secrets set "GooglePlaces:ApiKey" "XXXXXXXXXXXX"
dotnet user-secrets set "Gemini:ApiKey" "XXXXXXXXXXXX"
dotnet user-secrets set "AdminAccount:Email" "admin@exemplo.com"
dotnet user-secrets set "AdminAccount:Password" "UmaPasswordForte!123"
```

Para confirmar que os segredos foram guardados corretamente:

```bash
dotnet user-secrets list
```

> Em produção, estes valores devem ser fornecidos como **variáveis de ambiente** do sistema operativo ou do serviço de alojamento (ex: Azure App Settings), nunca em ficheiros de configuração versionados.

---

## 🗄️ Base de dados e migrations

A aplicação recria automaticamente o esquema da base de dados e o utilizador administrador inicial ao arrancar (ver `DbSeeder.cs`), mas as migrations também podem ser aplicadas manualmente:

```bash
dotnet ef database update
```

Se precisares de criar uma nova migration após alterar uma entidade:

```bash
dotnet ef migrations add NomeDaMigration
dotnet ef database update
```

> **Nota:** o ficheiro `LeadScoutCRM.db` (e os seus companheiros `-shm` e `-wal`, resultantes do modo *Write-Ahead Logging* do SQLite) **não são versionados** neste repositório, uma vez que contêm dados pessoais reais (emails, *hashes* de password, leads). O esquema completo está garantido pelas migrations em `Migrations/`.

---

## 💳 Stripe — testar pagamentos localmente

Para que os *webhooks* do Stripe cheguem à aplicação em ambiente local, usa o Stripe CLI:

```bash
stripe listen --forward-to https://localhost:{porta}/api/stripe/webhook
```

> ⚠️ **Importante:** o `whsec_...` (assinatura do *webhook*) apresentado no terminal muda **sempre** que reinicias o comando `stripe listen`. É necessário atualizar o valor em User Secrets sempre que isto acontecer:
>
> ```bash
> dotnet user-secrets set "Stripe:WebhookSecret" "whsec_NOVO_VALOR"
> ```

Para simular uma compra completa em modo de teste, usa os [cartões de teste do Stripe](https://stripe.com/docs/testing) (ex: `4242 4242 4242 4242`, qualquer data futura e CVC).

---

## ▶️ Correr a aplicação

```bash
dotnet run
```

Ou, a partir do Visual Studio, `F5` / `Ctrl+F5`.

Por omissão, a aplicação disponibiliza o **Swagger UI** em ambiente de desenvolvimento, acessível em `/swagger`, para explorar os *endpoints* da API REST (`/api/leads`, `/api/stripe/webhook`).

O utilizador administrador é criado automaticamente no primeiro arranque, com as credenciais definidas em `AdminAccount:Email` / `AdminAccount:Password` nos User Secrets.

---

## 💰 Planos e limites

| Plano | Preço/mês | Limite de leads | Kanban | Exportação CSV | Acesso API |
|---|---|---|---|---|---|
| Free | 0€ | 10 | ✅ | ❌ | ❌ |
| Pro | 19€ | Ilimitado | ✅ | ✅ | ❌ |
| Business | 49€ | Ilimitado | ✅ | ✅ | ✅ |

A ativação do plano é feita de forma assíncrona através do evento `checkout.session.completed` do Stripe, com fallback para `customer.subscription.created/updated` caso o *metadata* da sessão não esteja disponível.

---

## 🔒 Segurança

Medidas implementadas na aplicação:

- **Autenticação por cookies** com expiração deslizante (*sliding expiration*) de 7 dias
- **Hashing de passwords** com PBKDF2 (implementação nativa do ASP.NET Core Identity)
- **Bloqueio por força bruta** (*lockout*) após tentativas de login falhadas
- **Proteção CSRF** (`[ValidateAntiForgeryToken]`) em todos os formulários de escrita
- **Consultas parametrizadas** nativas do Entity Framework Core (proteção contra SQL Injection)
- **Codificação automática de HTML** pelo motor Razor (proteção contra XSS)
- **Verificação de assinatura** dos *webhooks* Stripe (`EventUtility.ConstructEvent`)
- **Isolamento multi-tenant** rigoroso — todas as consultas a `Leads` e `Notes` são filtradas por `UserId`

### Gestão de segredos

Nenhuma chave de API, *connection string* sensível ou credencial é armazenada em texto simples no repositório:

- Desenvolvimento → [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
- Produção → Variáveis de ambiente

O `.gitignore` do projeto exclui explicitamente `appsettings.Development.json`, ficheiros `*.db` e diretórios de build (`bin/`, `obj/`).

---

## 🧠 Notas de arquitetura

Algumas decisões técnicas relevantes, documentadas para referência futura:

- **RequestModels vs. ViewModels**: o projeto separa deliberadamente os modelos de entrada da API (`RequestModels.cs`) dos modelos usados pelas Views Razor (`ViewModels`), implementando o padrão DTO (*Data Transfer Object*) sob nomenclatura adaptada ao contexto de cada camada.
- **Prompt engineering (Gemini)**: os *prompts* de geração de mensagens injetam diretamente os dados reais do negócio (nome, nicho, cidade) e proíbem explicitamente marcadores de posição (`[Nome]`, `[Empresa]`), evitando que a IA devolva texto por preencher. É aplicado pós-processamento para remover formatação Markdown residual (`**`, `##`).
- **Enriquecimento paralelo**: os resultados da pesquisa Google Places são enriquecidos com detalhes adicionais (telefone, website) através de chamadas concorrentes com `Task.WhenAll`, reduzindo significativamente o tempo total de resposta face a um ciclo sequencial.

---

## 👤 Autor

Desenvolvido por João Caldara, no âmbito da PAP do curso TGPSI — INETE, 2026.

---

## 📄 Licença

Projeto académico desenvolvido para fins de avaliação da Prova de Aptidão Profissional. Uso e reprodução sujeitos a autorização do autor.
