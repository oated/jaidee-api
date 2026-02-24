# JaiDee -- Project Overview

## 💚 Overview

JaiDee is a LINE-based personal finance assistant designed to help users
become more mindful about their spending habits.

The core idea is simple:

> Record expenses via chat.\
> See clear summaries.\
> Receive gentle emotional reminders.

This project focuses on building a lean, scalable backend using Clean
Architecture principles.

------------------------------------------------------------------------

## 🎯 Problem Statement

Many people: - Spend money unconsciously - Run out of money before the
end of the month - Avoid complex finance apps - Feel judged by budgeting
tools

JaiDee solves this by: - Using LINE chat as the primary interface -
Providing soft, non-judgmental feedback - Making expense recording
effortless

------------------------------------------------------------------------

## 🧱 Architecture

This project uses Clean Architecture.

Solution structure:

JaiDee.sln ├── JaiDee.API ├── JaiDee.Application ├── JaiDee.Domain └──
JaiDee.Infrastructure

### Layer Responsibilities

**Domain** - Core entities (User, Transaction) - Enums
(TransactionType) - No external dependencies

**Application** - Use case interfaces - Business logic services - No EF
Core references

**Infrastructure** - EF Core implementation - PostgreSQL persistence -
Repository implementations

**API** - Controllers - Dependency injection - LINE webhook endpoint

------------------------------------------------------------------------

## 🗄 Database Schema

### Users

-   Id (Guid)
-   LineUserId (unique)
-   DisplayName
-   CreatedAt

### Transactions

-   Id (Guid)
-   UserId (FK)
-   Type (Income / Expense)
-   Amount (numeric 18,2)
-   Note
-   TransactionDate
-   CreatedAt

Indexes: - Unique(LineUserId) - Index(UserId) - Index(TransactionDate)

------------------------------------------------------------------------

## 🚀 MVP Features

-   Record income and expenses
-   Calculate monthly summary
-   Provide soft emotional responses
-   LINE webhook integration

------------------------------------------------------------------------

## 🔜 Future Roadmap

Phase 2: - Budget alerts - Advanced analytics - Premium plan (29
THB/month)

Phase 3: - Mobile app (Flutter) - AI-based financial insights - Fintech
integration

------------------------------------------------------------------------

## 💬 Emotional Design Principle

Every system response should follow this format:

\[Primary info\] \[Context summary\] \[Soft emotional ending\]

Example:

บันทึกแล้วนะ 💚\
วันนี้ใช้ไปแล้ว 450 บาท\
ค่อย ๆ ไปก็ได้นะ 🌿

------------------------------------------------------------------------

## 📌 Development Status

✔ Clean Architecture setup complete\
✔ EF Core configured\
✔ PostgreSQL connected\
🔜 Next: Implement TransactionService and LINE webhook logic

------------------------------------------------------------------------

## 📄 License

MIT
