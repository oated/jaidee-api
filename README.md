# JaiDee API

LINE-based personal finance assistant backend (Clean Architecture) สำหรับบันทึกรายรับ/รายจ่าย และสรุปรายเดือนผ่านแชต

## Features
- บันทึกรายรับ/รายจ่ายผ่าน API
- สรุปรายเดือน (`income/expense/balance/count`)
- LINE Webhook integration
- ตรวจสอบ `x-line-signature` (HMAC-SHA256)
- รองรับ Flex Message reply
- รองรับ flow แบบคุยทีละขั้น (stateful):
  - เริ่มจาก `รายรับ/รายจ่าย` หรือ `postback`
  - ถามชื่อรายการ -> ถามจำนวน -> ยืนยัน -> บันทึก
- Quick reply พร้อมไอคอน (configurable)
- Global exception handling + trace id

## Tech Stack
- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- LINE Messaging API

## Project Structure
- `JaiDee.API` - controllers, LINE integration, middleware
- `JaiDee.Application` - use cases / service interfaces
- `JaiDee.Domain` - entities, enums
- `JaiDee.Infrastructure` - EF Core, repositories, persistence

## Prerequisites
- .NET SDK 9.x
- PostgreSQL 16+
- LINE Official Account + Messaging API channel

## Configuration
ไฟล์หลัก:
- `JaiDee.API/appsettings.json`
- `JaiDee.API/appsettings.Development.json`

สำคัญ: ห้ามเก็บ secret จริงในไฟล์ json ที่ commit เข้า git

ค่าที่ต้องตั้ง:
- `ConnectionStrings:DefaultConnection`
- `LineBot:ChannelSecret`
- `LineBot:ChannelAccessToken`
- `LineBot:ApiBaseUrl` (ค่าเริ่มต้น `https://api.line.me/`)
- `LineBot:SkipSignatureValidation`
  - `true` สำหรับ local test
  - `false` สำหรับ production

optional quick reply settings:
- `LineBot:NoteQuickReplyCount`
- `LineBot:ExpenseNoteSuggestions`
- `LineBot:IncomeNoteSuggestions`
- `LineBot:QuickReplyIcons` (`label -> https image url`)

### Secret Management (Recommended)
ใช้ `dotnet user-secrets` สำหรับ local development:

```bash
dotnet user-secrets --project /Users/nathapotthaweepong/clap/jaidee-api/JaiDee.API/JaiDee.API.csproj set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=jaidee_db;Username=postgres;Password=postgres;"
dotnet user-secrets --project /Users/nathapotthaweepong/clap/jaidee-api/JaiDee.API/JaiDee.API.csproj set "LineBot:ChannelSecret" "<your-line-channel-secret>"
dotnet user-secrets --project /Users/nathapotthaweepong/clap/jaidee-api/JaiDee.API/JaiDee.API.csproj set "LineBot:ChannelAccessToken" "<your-line-channel-access-token>"
```

หรือใช้ environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=jaidee_db;Username=postgres;Password=postgres;"
export LineBot__ChannelSecret="<your-line-channel-secret>"
export LineBot__ChannelAccessToken="<your-line-channel-access-token>"
```

ถ้าเคยเผลอ commit token/secret จริงไปแล้ว ให้ rotate ที่ LINE Developers ทันที

## Setup
1. Restore + build
```bash
dotnet restore /Users/nathapotthaweepong/clap/jaidee-api/jaidee-api.sln
dotnet build /Users/nathapotthaweepong/clap/jaidee-api/jaidee-api.sln
```

2. Apply database migrations
```bash
dotnet ef database update \
  --project /Users/nathapotthaweepong/clap/jaidee-api/JaiDee.Infrastructure/JaiDee.Infrastructure.csproj \
  --startup-project /Users/nathapotthaweepong/clap/jaidee-api/JaiDee.API/JaiDee.API.csproj \
  --context AppDbContext
```

3. Run API
```bash
dotnet run --project /Users/nathapotthaweepong/clap/jaidee-api/JaiDee.API/JaiDee.API.csproj
```

## API Endpoints

### 1) Record transaction
`POST /api/transactions`

Request:
```json
{
  "lineUserId": "u_test_001",
  "displayName": "Nat",
  "type": 2,
  "amount": 120,
  "note": "coffee"
}
```

`type: 1 = Income, 2 = Expense`

### 2) Monthly summary
`GET /api/transactions/monthly-summary?lineUserId=u_test_001&year=2026&month=2`

### 3) LINE webhook
`POST /api/webhook/line`

รองรับ:
- text command: `summary`, `สรุป`, `สรุป 2/2026`, `สรุป 2026-2`
- start flow: `รายรับ`, `รายจ่าย`, `record_income`, `record_expense`
- state flow:
  - ขั้นชื่อรายการ: quick reply แนะนำรายการ (สุ่มจาก config)
  - ขั้นจำนวน: quick reply `100`, `200`, `500`, `ยกเลิก`
  - ขั้นยืนยัน: quick reply `ใช่`, `ยกเลิก`

## Rich Menu (Recommended)
เพื่อให้เมนูค้างด้านล่างตลอด ให้ใช้ Rich Menu (ไม่ใช่ quick reply)

แนะนำปุ่ม:
- `รายรับ` -> postback `record_income`
- `รายจ่าย` -> postback `record_expense`
- `สรุป` -> message `สรุป`

## Notes
- ใน production ควรใช้ HTTPS public endpoint และตั้งค่า Webhook URL ใน LINE Developers
- ควรย้าย secret ไป environment variables หรือ secret manager
- ห้าม commit token จริงลง git

## Troubleshooting
- `404` ตอนยิง API: ตรวจว่ารัน `JaiDee.API` project ถูกตัว
- `Invalid LINE signature`: ตรวจ `ChannelSecret` และ `SkipSignatureValidation`
- `TaskCanceledException` ตอน reply: เป็น network/cancel ได้ ระบบจะ log warning และไม่ควรทำให้ webhook ล้มทั้งคำขอ

## License
MIT
