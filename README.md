# E-Commerce-API-V1

A modular, extendable API for e-commerce platforms, built in C#. This repository provides a foundation for managing products, categories, inventories, shopping carts, orders, payment integrations, and other services, following best practices and modern architectural patterns.

---

## Table of Contents

- [Features](#features)
- [Technologies](#technologies)
- [Getting Started](#getting-started)
- [API Documentation](#api-documentation)
- [Database Migration](#database-migration)
- [Example API Requests](#example-api-requests)
- [Project Architecture](#project-architecture)
- [SOLID Principles](#solid-principles)
- [Testing](#testing)
- [Contributing](#contributing)
- [License](#license)
- [Author](#author)

---

## Features

- **Generic Repository Pattern:** Easily manage entities (CRUD, soft delete, restore, batch updates).
- **Category & Subcategory Management:** Activate/deactivate, check for images, and control product relationships.
- **Shopping Cart System:** User-specific cart, add/remove/clear items, check for emptiness, and count items.
- **Payment Integration:** Easily connect and manage payment gateways for processing orders.
- **Order Services:** Place, update, and track orders with extensible service architecture.
- **Redis Caching:** Improve performance and scalability with Redis as a distributed cache.
- **Soft Delete & Restore:** Entities can be soft-deleted and restored, keeping your data safe.
- **Logging:** Each repository action is logged for easier debugging and monitoring.
- **SOLID Principles:** The codebase is designed with SOLID principles for maintainability, scalability, and testability.

---

## Technologies

- **Primary Language:** C#
- **Database:** MySQL (via Entity Framework Core)
- **Cache:** Redis
- **Other:** Dapper, StackExchange.Redis, Microsoft.Extensions.Logging, Newtonsoft.Json

---

## Getting Started

### Prerequisites

- .NET SDK (latest recommended)
- MySQL database
- Redis (for caching)
- Docker (optional, Dockerfile included)

### Installation

```bash
git clone https://github.com/omargamal1121/E-Commerce-API-V1.git
cd E-Commerce-API-V1
# Configure your appsettings.json for MySQL and Redis
dotnet build
dotnet run
```

You can also build and run with Docker:

```bash
docker build -t ecommerce-api .
docker run -p 5000:80 ecommerce-api
```

---

## API Documentation

Interactive API documentation is provided with Swagger.

- **Local Development:**  
  Visit [http://localhost:5000/swagger](http://localhost:5000/swagger)
- **Live Swagger (Deployed):**  
  [https://e-commerce-api-v1-p515.onrender.com/swagger/index.html](https://e-commerce-api-v1-p515.onrender.com/swagger/index.html)

---

## Database Migration

Ensure your MySQL database schema is up to date using Entity Framework migrations:

```bash
dotnet ef database update
```

- Make sure your connection string is set for MySQL in `appsettings.json`.
- You may need to install the EF Tools:  
  `dotnet tool install --global dotnet-ef`

---

## Example API Requests

Below are some example requests and responses using cURL.

### Add a Product

```bash
curl -X POST "http://localhost:5000/api/products" \
     -H "Content-Type: application/json" \
     -d '{
           "name": "New Product",
           "price": 49.99,
           "categoryId": 1,
           "description": "A sample product"
         }'
```

**Response**
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

### Get a Product

```bash
curl "http://localhost:5000/api/products/123"
```

**Response**
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

## Project Architecture

```plaintext
Controller
    ↓
Service
    ↓
Repository
    ↓
DbContext (EF Core)
```
- **Controller:** Handles HTTP requests and responses.
- **Service:** Contains business logic and orchestrates repository calls (including Payment and Order services).
- **Repository:** Handles all data access and CRUD operations.
- **DbContext:** Manages entity framework and database interactions.

---

## SOLID Principles

This project is designed using the SOLID principles:

- **S**ingle Responsibility Principle: Each class has one responsibility.
- **O**pen/Closed Principle: Classes are open for extension, closed for modification.
- **L**iskov Substitution Principle: Subtypes can be substituted for their base types without breaking the app.
- **I**nterface Segregation Principle: Interfaces are specific and not bloated.
- **D**ependency Inversion Principle: High-level modules depend on abstractions, not on concrete implementations.

---

## Testing

Unit and integration tests are planned.  
The project structure is ready for xUnit/NUnit integration.  
_You can add unit and integration tests using xUnit, NUnit, or your preferred .NET testing framework!_

---

## Contributing

Pull requests and issues are welcome! Please fork the repository and submit your PRs to the `main` branch.

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
  - [LinkedIn](https://www.linkedin.com/in/omar-gamal-2a55812b5/)

---

> For more details, explore the repository source code and documentation.