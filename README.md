# E-Commerce-API-V1

[![Build Status](https://img.shields.io/github/actions/workflow/status/omargamal1121/E-Commerce-API-V1/main.yml?branch=main)](https://github.com/omargamal1121/E-Commerce-API-V1/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tech: C#, MySQL, Redis, JWT](https://img.shields.io/badge/Tech-C%23%2C%20MySQL%2C%20Redis%2C%20JWT%2C%20Docker-blue)](#technologies)

A robust, modular, and extensible API for e-commerce platforms built using **C# (.NET Core)**.  
This repository provides scalable solutions for managing products, categories, inventories, carts, orders, payment integrations, and more. It leverages **Clean Architecture** for maintainability and **JWT authentication** for secure API access.

---

## Table of Contents

- [Features](#features)
- [Badges & Tech Stack](#badges--tech-stack)
- [Architecture Diagram](#architecture-diagram)
- [Modules Overview](#modules-overview)
- [Security](#security)
- [Caching Strategy](#caching-strategy)
- [Getting Started](#getting-started)
- [API Documentation](#api-documentation)
- [Example API Requests](#example-api-requests)
- [CI/CD & Deployment](#cicd--deployment)
- [Clean Architecture](#clean-architecture)
- [SOLID Principles](#solid-principles)
- [Testing](#testing)
- [Contributing](#contributing)
- [License](#license)
- [Author](#author)

---

## Features

- Efficient **Generic Repository Pattern** for CRUD, soft delete, restore, batch updates.
- **Category & Subcategory Management:** Status toggling, image checking, product relationships.
- **Shopping Cart System:** User-centric, supports add/remove/clear/count.
- **Payment Integration:** Easily connect and manage payment gateways.
- **Order Services:** Place, update, and track orders; extendable service architecture.
- **Redis Caching** for performance optimization and scalability.
- **JWT Authentication & Authorization** for secure API access and token-based authentication.
- **Entity Soft Delete & Restore:** Data integrity by reversible deletes.
- **Structured Logging** for debugging and operational clarity.
- Adheres strictly to **SOLID** and **Clean Architecture** principles for reliability.

---

## Badges & Tech Stack

- Build: [![Build Status](https://img.shields.io/github/actions/workflow/status/omargamal1121/E-Commerce-API-V1/main.yml?branch=main)](https://github.com/omargamal1121/E-Commerce-API-V1/actions)
- License: [![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
- Tech Stack: [![Tech: C#, MySQL, Redis, JWT, Docker](https://img.shields.io/badge/Tech-C%23%2C%20MySQL%2C%20Redis%2C%20JWT%2C%20Docker-blue)](#technologies)

**Primary Technologies:**
- **Language:** C#
- **Database:** MySQL via Entity Framework Core
- **Cache:** Redis
- **Authentication:** JWT (JSON Web Tokens)
- **Other:** Dapper, StackExchange.Redis, Microsoft.Extensions.Logging, Newtonsoft.Json
- **Containerization:** Docker

---

## Architecture Diagram


```mermaid
graph TD
    PresentationLayer["Presentation Layer (Controllers)"] --> ApplicationLayer["Application Layer (Services & Use Cases)"]
    ApplicationLayer --> DomainLayer["Domain Layer (Entities & Business Rules)"]
    DomainLayer --> InfrastructureLayer["Infrastructure Layer (Repositories, Persistence, Integrations)"]
    InfrastructureLayer --> ExternalSystems["External Systems"]
    PresentationLayer -.->|JWT Validation| SecurityMiddleware["Security Middleware"]
    ApplicationLayer -.->|Cache Operations| RedisCache["Redis Cache"]
```

---

## Modules Overview

| Module           | Description                                                                                  |
|------------------|---------------------------------------------------------------------------------------------|
| **Domain**       | Contains business models and core rules, representing products, users, categories, etc.     |
| **Application**  | Defines service interfaces, use cases, data transfer objects, and orchestrates workflows.    |
| **Infrastructure** | Handles data persistence, repository implementation, payment APIs, and cache strategies.   |
| **Presentation** | Hosts API controllers/endpoints, managing HTTP requests and responses via RESTful patterns.  |

---

## Security

### JWT Authentication

The API implements **JSON Web Token (JWT)** authentication for secure API access:

- **Token Generation:** Users receive JWT tokens upon successful login
- **Token Validation:** All protected endpoints validate JWT tokens in the Authorization header
- **Token Expiration:** Configurable token expiration times for enhanced security
- **Claim-based Authorization:** Role-based access control (RBAC) using JWT claims

**Bearer Token Format:**
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Security Features

- Secure password hashing with industry standards
- Protected sensitive endpoints requiring authentication
- Support for role-based access control (Admin, User)
- Token refresh mechanism for session management
- External login integration support (OAuth providers)

---

## Caching Strategy

### Redis Implementation

The API leverages **Redis** for high-performance caching:

**Cached Data:**
- Product listings and details
- Category information
- User shopping carts
- Order history summaries
- User sessions and authentication tokens

**Cache Benefits:**
- Reduced database load
- Faster response times for frequently accessed data
- Improved scalability during peak traffic
- Session management and token blacklisting support

**Configuration:**
```json
{
  "Redis": {
    "Connection": "localhost:6379",
    "DefaultExpiration": 3600
  }
}
```

**Cache Invalidation:**
- Automatic expiration based on TTL (Time To Live)
- Manual invalidation on data updates
- Cascade invalidation for dependent data

---

## Getting Started

### Prerequisites

- .NET SDK (latest stable)
- MySQL database
- Redis server
- Docker (recommended for full stack setup)

### Local Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/omargamal1121/E-Commerce-API-V1.git
   cd E-Commerce-API-V1
   ```

2. **Configure environment**
   - Update `appsettings.json` with your MySQL and Redis details.
   - Configure JWT secret key in `appsettings.json`:
     ```json
     {
       "Jwt": {
         "SecretKey": "your-secret-key-min-32-characters",
         "Issuer": "YourAppName",
         "Audience": "YourAppUsers",
         "ExpirationMinutes": 60
       },
       "Redis": {
         "Connection": "localhost:6379"
       }
     }
     ```
   - Optionally, set environment variables as needed.

3. **Restore & Build**
   ```bash
   dotnet restore
   dotnet build
   ```

4. **Database Migration**
   ```bash
   dotnet ef database update
   ```
   > Make sure [dotnet-ef](https://docs.microsoft.com/en-us/ef/core/cli/dotnet) is installed:  
   > `dotnet tool install --global dotnet-ef`

5. **Run Locally**
   ```bash
   dotnet run --project src/Presentation
   ```
   API will be accessible at [http://localhost:5000/swagger](http://localhost:5000/swagger).

### Docker Setup & Configuration

This project is fully containerized using **Docker** and orchestrated via **Docker Compose**. It spins up three services:
1. **API:** The C# ASP.NET Core Web API container (`e-commerce-api`).
2. **Database:** A MySQL 8.0 container (`mysql`) with persistent data volumes.
3. **Cache:** A Redis container (`redis`) for caching.

#### 1. Configure Environment Variables

Before running Docker Compose, you must set up your environment variables. 

1. Copy the `.env.example` file to create a `.env` file in the project root:
   ```bash
   cp .env.example .env
   ```
2. Open `.env` and fill in your actual settings (passwords, JWT keys, Cloudinary credentials, SMTP secrets, etc.).

> [!IMPORTANT]
> The `.env` file is excluded from git version control via `.gitignore` and from the Docker image context via `.dockerignore` to ensure your production secrets remain safe.

#### 2. ASP.NET Core Configuration Naming Convention

This setup utilizes environment variables to override values in `appsettings.json`. ASP.NET Core uses a **double underscore (`__`)** as a separator to map to nested JSON hierarchy.
For example:
* `ConnectionStrings__DBbyMonster` maps to `ConnectionStrings:DBbyMonster`
* `Jwt__Key` maps to `Jwt:Key`
* `CloudinarySettings__CloudName` maps to `CloudinarySettings:CloudName`

These environment variables are declared in `.env` and passed to the API container via `Docker-Compose.yml`.

#### 3. Run the Entire Stack

To build the API image and start all containers in detached mode, run:
```bash
docker compose up --build -d
```

* **MySQL Healthchecks:** The API container uses advanced `depends_on` rules. It will wait to start until the MySQL container is fully ready and healthy (verified via `mysqladmin ping`).
* **Automatic Migrations:** On startup, the API container automatically applies any pending Entity Framework Core migrations to the MySQL database via `dbContext.Database.Migrate()` inside `Program.cs`.

#### 4. Managing the Containers

* **Stop the stack:**
  ```bash
  docker compose down
  ```
* **Stop and delete persistent volumes:**
  ```bash
  docker compose down -v
  ```
* **View logs:**
  ```bash
  docker compose logs -f api
  ```
* **Check container status:**
  ```bash
  docker compose ps
  ```

API Swagger documentation will be accessible locally at `http://localhost:7288/swagger` (HTTP) or `http://localhost:7289/swagger` (HTTPS / if configured).

---

## API Documentation

Explore the API endpoints interactively:

- **Local:** [http://localhost:5000/swagger](http://localhost:5000/swagger)
- **Hosted:** [Live Swagger UI](https://e-commerce-api-v1-p515.onrender.com/swagger/index.html)

---

## Example API Requests

#### User Login (Obtain JWT Token)

```bash
curl -X POST "http://localhost:5000/api/auth/login" \
     -H "Content-Type: application/json" \
     -d '{
           "email": "user@example.com",
           "password": "securePassword123"
         }'
```

**Sample Response**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "userId": 1,
  "email": "user@example.com"
}
```

#### Add a Product (Requires JWT)

```bash
curl -X POST "http://localhost:5000/api/products" \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer YOUR_JWT_TOKEN" \
     -d '{
           "name": "New Product",
           "price": 49.99,
           "categoryId": 1,
           "description": "A sample product"
         }'
```

**Sample Response**
```json
{
  "id": 123,
  "name": "New Product",
  "price": 49.99,
  "categoryId": 1,
  "description": "A sample product",
  "createdAt": "2025-09-01T12:00:00Z"
}
```

#### Get a Product (Cached Response)

```bash
curl "http://localhost:5000/api/products/123" \
     -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Sample Response** (served from Redis cache)
```json
{
  "id": 123,
  "name": "New Product",
  "price": 49.99,
  "categoryId": 1,
  "description": "A sample product",
  "createdAt": "2025-09-01T12:00:00Z"
}
```

---

## CI/CD & Deployment

- **CI:** GitHub Actions validates commits with build & test workflows.
- **CD:** Deployment can leverage staging environments and blue/green strategies for zero-downtime upgrades.
- **Safety:** All changes go through automated tests; production releases require approval to minimize risk.

**Example Workflow:**
- PR opened → Build/Test → Deployed to Staging (auto/preview)
- Manual approval → Blue/Green deployment → Traffic switched to new release after health checks

---

## Clean Architecture

> Decoupling for Scale & Quality

The project is organized into four distinct layers:

- **Domain:** Core business logic, entities, and rules.
- **Application:** Application workflows, services, use case orchestration.
- **Infrastructure:** Data access, external integrations, and technical details.
- **Presentation:** Web API controllers and HTTP endpoints.

**Benefits:** Testable code, easy maintenance, flexible technology adoption, and clear separation of concerns.

---

## SOLID Principles

The entire codebase is structured with SOLID in mind:
- **S:** Each class or module has a single responsibility.
- **O:** All entities are open for extension, closed for modification.
- **L:** Subtypes substitute their base types without breaking logic.
- **I:** Interfaces segregated for focused implementation.
- **D:** High-level logic is built on abstractions, not concrete classes.

---

## Testing

The solution is ready for unit and integration tests via xUnit or NUnit.  
Coverage of business rules, data access, and controller contracts is recommended for quality assurance.

---

## Contributing

Contributions are welcome! Please follow these guidelines:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/YourFeature`)
3. Commit your changes (`git commit -m 'Add YourFeature'`)
4. Push to the branch (`git push origin feature/YourFeature`)
5. Open a Pull Request

---

## License

MIT License

```
MIT License

Copyright (c) 2025 omargamal1121

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## Author

- **Omar Gamal**
  - [GitHub](https://github.com/omargamal1121)
  - [LinkedIn](https://www.linkedin.com/in/omar-gamal-226232292/)

---

> For details, see source code, documentation, or reach out via issues.
