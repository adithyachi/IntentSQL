# IntentSQL

> **An AI-powered, schema-aware Natural Language to SQL agent that translates business questions into validated SQL and queries live relational data.**

🌐 **[Try the Live Demo →](https://adithya-chiruvolu-intentsql.jollyocean-48e21c96.canadacentral.azurecontainerapps.io/)**

---

## 📸 IntentSQL in Action

### 1. IntentSQL Overview

IntentSQL provides a simple interface for exploring relational data using natural language.

![IntentSQL Home](docs/images/01-intentsql-home.png)

---

### 2. Ask a Business Question

Users can ask analytical questions in natural language. IntentSQL interprets the request and generates the appropriate SQL query.

![Ask Business Question](docs/images/02-ask-business-question.png)

---

### 3. AI Execution Details & Performance

Each execution provides visibility into AI processing, including token usage, response time, total processing time, and SQL generation attempts.

![AI Execution Details](docs/images/03-ai-execution-details.png)

---

### 4. Generated SQL and Results

IntentSQL records SQL generation attempts and displays the generated SQL used to query the relational database and return results.

![Generated SQL and Results](docs/images/04-generated-sql-results.png)

---

## 🎯 The Problem

Business users often need answers from relational databases but may not know:

- Which tables contain the required data
- How tables are related
- Which columns should be used
- How to write SQL joins and aggregations
- How to correctly calculate business metrics

Traditional applications typically require developers to build a separate report, API endpoint, or query for every business question.

**IntentSQL explores a different approach:**

> Allow users to ask business questions naturally and dynamically translate those questions into queries against live relational data.

For example:

> **Which country has the highest average total value per completed order?**

To answer this correctly, the system must understand:

- The database schema
- Relevant tables and relationships
- Business definitions
- Aggregation logic
- SQL syntax and execution requirements

---

## 🧠 How It Works

```text
Business Question
       │
       ▼
Dynamic Schema Discovery
       │
       ▼
Business Semantics + SQL Rules
       │
       ▼
AI Provider
(Ollama / Together AI)
       │
       ▼
Schema-Aware SQL Generation
       │
       ▼
SQL Safety Validation
       │
       ▼
PostgreSQL Execution
       │
       ├──────────── Success ────────────► Results
       │
       ▼
Database Execution Error
       │
       ▼
Bounded SQL Correction
       │
       ▼
Retry
```

IntentSQL goes beyond simply sending a question to an LLM and accepting generated SQL.

The workflow combines:

- 🔎 Dynamic database schema discovery
- 🧠 Schema-aware AI prompting
- 📐 Business semantics and SQL rules
- 🛡 Read-only SQL validation
- 🔄 Database error feedback
- 🎯 Bounded SQL correction attempts

---

## ✨ Key Capabilities

### 🔎 Dynamic Database Schema Discovery

IntentSQL dynamically discovers information from the live PostgreSQL database, including:

- Tables
- Columns
- Data types
- Primary keys
- Foreign-key relationships
- Unique constraints
- Indexes

This allows the AI workflow to work with the actual database structure rather than relying entirely on manually maintained schema descriptions.

---

### 🧠 Schema-Aware SQL Generation

Before generating SQL, IntentSQL builds database context for the AI model.

The SQL generation workflow is designed to ensure that generated SQL:

- Uses real tables and columns
- Uses valid relationships
- Produces PostgreSQL-compatible SQL
- Generates a single query
- Avoids inventing database objects

The schema context can also include explicit business semantics and SQL-generation guidance.

---

### 🤖 Multi-Provider AI Architecture

IntentSQL uses an AI provider abstraction rather than tightly coupling the application to a single AI provider.

Current implementations include:

- **Ollama** — local model execution
- **Together AI** — hosted model access

This architecture allows experimentation with different models and providers behind a common application interface.

---

### 🛡 Read-Only SQL Safety Validation

Generated SQL is validated before execution.

The current validation model:

- Allows `SELECT`
- Allows CTE-based queries beginning with `WITH`
- Rejects multiple statements
- Blocks potentially destructive operations

Examples of blocked operations include:

```text
INSERT
UPDATE
DELETE
DROP
ALTER
TRUNCATE
CREATE
GRANT
REVOKE
MERGE
```

This helps ensure that AI-generated SQL cannot modify the database.

---

### 🔄 Bounded SQL Correction

AI-generated SQL is not always correct on the first attempt.

When a query fails during execution, IntentSQL can use database feedback to generate a corrected query.

The correction context can include:

- Original user question
- Database schema context
- Previous SQL attempts
- Database error information
- Correction attempt number

Later correction attempts are designed to avoid simply repeating previously failed SQL or the same invalid strategy.

```text
Generate SQL
     │
     ▼
Validate SQL
     │
     ▼
Execute SQL
     │
 ┌───┴────┐
 │        │
Success   Error
 │        │
 ▼        ▼
Results  Correction Context
              │
              ▼
        Generate Corrected SQL
              │
              ▼
            Retry
```

---

## 🏗 Architecture Overview

```text
┌──────────────────────────────────┐
│        ASP.NET Core MVC          │
│                                  │
│          User Question           │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│      SQL Generation Service      │
│                                  │
│  • Builds AI prompts             │
│  • Uses schema context           │
│  • Applies SQL constraints       │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│      AI Provider Abstraction     │
└────────────┬─────────────┬───────┘
             │             │
             ▼             ▼
        ┌─────────┐   ┌─────────────┐
        │ Ollama  │   │ Together AI │
        └─────────┘   └─────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│         Generated SQL            │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│         SQL Validation           │
│                                  │
│  • Read-only queries             │
│  • Block unsafe operations       │
│  • Single statement validation   │
└────────────────┬─────────────────┘
                 │
                 ▼
┌──────────────────────────────────┐
│       PostgreSQL Database        │
│                                  │
│  • Live schema                   │
│  • Live relational data          │
└────────────────┬─────────────────┘
                 │
         ┌───────┴────────┐
         │                │
         ▼                ▼
      Results      Execution Error
                         │
                         ▼
                  SQL Correction
                         │
                         ▼
                       Retry
```

---

## 🧩 Core Components

### ASP.NET Core MVC Application

The primary application responsible for:

- User interaction
- AI workflow orchestration
- SQL generation
- SQL execution
- Result presentation

### Database Schema Service

Discovers and describes the live database structure.

### SQL Generation Service

Builds schema-aware AI prompts and generates SQL.

### SQL Execution Service

Validates and executes read-only SQL queries.

### AI Provider Services

Current provider implementations include:

- Ollama
- Together AI

### Database Seeder

A separate project is included for populating the database with sample data.

### Docker Support

The solution includes:

- Application Dockerfile
- Database migration Dockerfile
- Database seeder Dockerfile
- Docker Compose configuration

---

## 💬 Example Questions

IntentSQL is designed to handle questions such as:

### Aggregation

> What is the total revenue generated in 2025?

### Comparison

> Which product category generated the highest revenue?

### Ranking

> Show the top 5 customers by completed-order revenue.

### Time-Based Analysis

> Compare completed-order revenue in 2025 with 2024.

### Complex Business Analysis

> For each product category, show completed-order revenue for 2025, its share of total completed-order revenue, percentage change compared with 2024, and rank the categories by 2025 revenue.

These questions can require:

- Multiple joins
- Aggregations
- Common Table Expressions
- Historical comparisons
- Percentage calculations
- Ranking

---

## 🛠 Technology Stack

| Area | Technologies |
|---|---|
| **Backend** | ASP.NET Core MVC, .NET 8, C# |
| **Database** | PostgreSQL, Entity Framework Core, Npgsql |
| **AI** | Ollama, Together AI, Qwen models |
| **Infrastructure** | Docker, Docker Compose |

---

## 📁 Repository Structure

```text
IntentSQL/
│
├── BizPulse.AI.POC/
│   ├── Controllers/
│   ├── Data/
│   ├── Models/
│   ├── Services/
│   ├── Views/
│   └── wwwroot/
│
├── BizPulse.AI.POC.DatabaseSeeder/
│
├── Dockerfile
├── Dockerfile.migration
├── Dockerfile.seeder
├── docker-compose.yml
├── .env.example
└── BizPulse.AI.POC.sln
```

> **Note:** The public repository is named **IntentSQL**. Some internal project names retain the original naming used during early development.

---

## 🚀 Running Locally

### Prerequisites

You will need:

- .NET 8 SDK
- Docker Desktop
- An AI provider configuration

Depending on the provider:

- Ollama running locally, or
- A Together AI API key

---

### 1. Clone the Repository

```bash
git clone https://github.com/adithyachi/IntentSQL.git
cd IntentSQL
```

---

### 2. Configure Environment Variables

Copy the example environment configuration:

```text
.env.example
```

Create your own local:

```text
.env
```

Example:

```text
POSTGRES_DB=bizpulse
POSTGRES_USER=bizpulse
POSTGRES_PASSWORD=your_local_password
```

> Never commit `.env` files containing local passwords or secrets.

---

### 3. Start PostgreSQL

```bash
docker compose up -d
```

---

### 4. Configure the Application

Configure your local database and AI provider settings.

Do not commit:

- API keys
- Passwords
- Tokens
- Local secrets

---

### 5. Run the Application

Open:

```text
BizPulse.AI.POC.sln
```

Then run the ASP.NET Core MVC application.

---

## 🔐 Security Considerations

IntentSQL is an AI engineering project and should not be treated as a production-ready database access system without additional production controls.

Local secrets should remain outside source control.

The repository uses local configuration patterns such as:

```text
.env
.env.*
appsettings.Local.json
```

Generated SQL is validated using a read-only execution model before database execution.

---

## 🎯 Why I Built This

IntentSQL was built as a hands-on AI engineering project to explore a real-world problem:

> **How can an AI system safely translate natural-language business questions into queries against live relational databases?**

The project explores challenges beyond basic LLM integration, including:

- Dynamic schema awareness
- AI provider abstraction
- Prompt constraints
- SQL safety
- Database execution feedback
- Bounded query correction
- Business semantics

The goal is to build practical understanding of how AI systems can interact with structured relational data.

---

## 🔮 Future Areas of Exploration

Potential future areas include:

- Retrieval-based schema context selection for larger databases
- Improved semantic validation of query results
- More advanced SQL validation
- Role-based database access controls
- Query cost and performance controls
- Evaluation datasets and benchmark scenarios
- Automated accuracy measurement
- Support for additional databases
- Additional AI providers and models

---

## ⚠️ Project Status

**IntentSQL is an active AI engineering project focused on experimentation, learning, and architecture exploration.**

It should not be considered production-ready without additional work around:

- Authentication and authorization
- Fine-grained database permissions
- Query resource limits
- Production security controls
- Monitoring and observability
- Comprehensive evaluation and testing

---

## 👨‍💻 Author

**Adithya Chiruvolu**

**Technology Leadership · .NET · Azure · AI Engineering**

---

> **IntentSQL explores how AI agents can bridge the gap between natural-language business intent and structured relational data.**
