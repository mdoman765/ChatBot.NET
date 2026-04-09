# PRAN-RFL WhatsApp Chatbot Backend API

ASP.NET Core 8 backend that powers the **PRAN-RFL Sales Support WhatsApp Chatbot**. It manages conversation sessions, stores incoming voice and image messages, handles complaint submission, and forwards complaints to the CRM.

---

## Tech Stack

| | |
|---|---|
| Framework | ASP.NET Core 8 |
| Database | Microsoft SQL Server |
| ORM | Entity Framework Core |
| WhatsApp Gateway | 360dialog |
| Workflow Automation | n8n |
| CRM | PRAN-RFL Internal CRM (`crm.prangroup.com`) |
| Staff Verification | PRAN HRIS API |

---

## Architecture Overview

```
WhatsApp User
      │
      ▼
360dialog (WhatsApp Gateway)
      │  webhook
      ▼
n8n Workflow
      │  HTTP calls
      ├──► POST /api/whatsapp/session          (read/write conversation state)
      ├──► POST /api/whatsapp/messages/voice   (upload voice note binary)
      ├──► POST /api/whatsapp/messages/image   (upload image binary)
      └──► POST /api/whatsapp/complaints/submit
                    │
                    ├── Save complaint + media to SQL Server
                    └── Forward to CRM API (multipart with actual files)
```

---

## Project Structure

```
crud-app-backend/
├── Controllers/
│   ├── WhatsAppSessionController.cs      # Session CRUD
│   ├── WhatsAppMessageController.cs      # Voice / image / text storage
│   └── WhatsAppComplaintController.cs    # Complaint submit + list + details
├── Services/
│   ├── WhatsAppSessionService.cs
│   ├── WhatsAppMessageService.cs         # Saves files to wwwroot/wa-media/
│   └── WhatsAppComplaintService.cs       # Loads files from disk → sends to CRM
├── Repositories/
│   ├── WhatsAppSessionRepository.cs
│   ├── WhatsAppMessageRepository.cs
│   └── WhatsAppComplaintRepository.cs
├── Models/
│   ├── WhatsAppSession.cs
│   ├── WhatsAppSessionHistory.cs
│   ├── WhatsAppMessage.cs
│   ├── WhatsAppComplaint.cs
│   └── WhatsAppComplaintMedia.cs
├── DTOs/
│   ├── SubmitComplaintRequestDto.cs
│   └── SubmitComplaintResponseDto.cs
├── AppDbContext.cs
├── Program.cs
└── appsettings.json
```

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local or remote)
- n8n instance connected to 360dialog

### 1. Clone

```bash
git clone https://github.com/your-org/pran-rfl-whatsapp-backend.git
cd pran-rfl-whatsapp-backend
```

### 2. Configure `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=CrudAppDB;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  },
  "Crm": {
    "SubmitUrl": "http://crm.prangroup.com/api/whats-app/sales-support/v1/create",
    "ApiKey":    "uH6rJ3QpW9xN2Tz5K8bL"
  }
}
```

### 3. Create database tables

Run these SQL scripts in order:

```bash
# Sessions and message tables
sqlcmd -S localhost -d CrudAppDB -i whatsapp_session.sql

# Complaint tables
sqlcmd -S localhost -d CrudAppDB -i whatsapp_complaint_migration.sql

# Add CRM ticket ID column (if upgrading)
sqlcmd -S localhost -d CrudAppDB -i add_crm_ticket_id.sql
```

### 4. Run

```bash
dotnet run
```

API is available at `http://localhost:5000`. Swagger UI at `http://localhost:5000/swagger`.

---

## API Reference

### Session

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/whatsapp/session?phone=8801...` | Get session state for a phone number |
| `POST` | `/api/whatsapp/session` | Create or update session (called by n8n after every message) |
| `DELETE` | `/api/whatsapp/session?phone=8801...` | Delete session (reset conversation) |
| `GET` | `/api/whatsapp/session/history?phone=8801...&limit=20` | Get conversation history |

**POST /api/whatsapp/session — Request body:**
```json
{
  "Phone":         "8801704134097",
  "CurrentStep":   "MAIN_MENU",
  "PreviousStep":  "AWAITING_STAFF_ID",
  "PendingReport": false,
  "PendingShopReg": false,
  "RawMessage":    "1",
  "TempData":      "{\"staff_id\":\"359778\",\"staff_verified\":true,...}"
}
```

---

### Messages

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/whatsapp/messages/text` | Store incoming text message |
| `POST` | `/api/whatsapp/messages/voice` | Upload voice note binary (multipart/form-data, max 25 MB) |
| `POST` | `/api/whatsapp/messages/image` | Upload image binary (multipart/form-data, max 16 MB) |
| `GET` | `/api/whatsapp/messages?phone=8801...&limit=20` | List recent messages |

**POST /api/whatsapp/messages/voice — Form fields:**

| Field | Type | Description |
|-------|------|-------------|
| `file` | file | Binary audio file |
| `messageId` | text | 360dialog message ID (wamid.xxx) |
| `from` | text | Sender phone number |
| `senderName` | text | Display name |
| `mimeType` | text | e.g. `audio/ogg` |
| `timestamp` | text | Unix timestamp |

Files are saved to `wwwroot/wa-media/audio/{messageId}.ogg` and `wwwroot/wa-media/images/{messageId}.jpg`.

---

### Complaints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/whatsapp/complaints/submit` | Submit complaint — saves to DB and forwards to CRM |
| `GET` | `/api/whatsapp/complaints` | List complaints (filter by phone, staff_id, status) |
| `GET` | `/api/whatsapp/complaints/{id}` | Get single complaint with media list (id or PR00007) |

**POST /api/whatsapp/complaints/submit — Request body:**
```json
{
  "whatsapp_phone":    "8801704134097",
  "staff_id":          "359778",
  "name":              "Sheikh Shariar Newaz",
  "official_phone":    "01704137508",
  "designation":       "Manager",
  "dept":              "Quality Control",
  "groupname":         "PRAN GROUP",
  "company":           "CS PRAN",
  "locationname":      "Habiganj Industrial Park",
  "email":             "user@prangroup.com",
  "description":       "Product was damaged on delivery",
  "voice_message_ids": ["wamid.HBgNODgw...AAA==", "wamid.HBgNODgw...BBB=="],
  "image_message_ids": ["wamid.HBgNODgw...CCC=="]
}
```

**What the service does:**
1. Saves complaint row to `WhatsAppComplaints` table
2. Assigns ticket number `PR00001`, `PR00002`, …
3. Reads each voice/image file from disk using the stored messageId
4. Posts all files + text fields to CRM as `multipart/form-data`
5. Stores the CRM ticket ID back in the database
6. Returns CRM ticket ID to n8n → bot tells the user

**Response:**
```json
{
  "success":      true,
  "complaint_id": "13",
  "message":      "Complaint submitted to support team"
}
```

**GET /api/whatsapp/complaints — Query params:**

| Param | Description |
|-------|-------------|
| `phone` | Filter by WhatsApp phone |
| `staff_id` | Filter by staff ID |
| `status` | Filter by status: `open` / `in_progress` / `resolved` / `closed` |
| `limit` | Max rows (default 50, max 200) |

---

## Database Schema

```
WhatsAppSessions          — one row per phone number (conversation state)
WhatsAppSessionHistory    — full audit trail of every state transition
WhatsAppMessages          — every incoming text/voice/image with file path
WhatsAppComplaints        — one row per submitted complaint
WhatsAppComplaintMedia    — one row per voice/image attached to a complaint
```

---

## How Files Are Stored

When n8n uploads a voice note or image:

```
POST /api/whatsapp/messages/voice
  → saved to: wwwroot/wa-media/audio/wamid.HBgN...AAA==.ogg
  → DB row:   WhatsAppMessages.FileUrl = "http://host/wa-media/audio/wamid.xxx.ogg"
              WhatsAppMessages.MessageId = "wamid.HBgN...AAA=="
```

When complaint is submitted, the service finds the file directly:

```csharp
Path.Combine(WebRootPath, "wa-media", "audio", $"{messageId}.ogg")
```

No URL parsing needed — path is always predictable from the messageId.

---

## CRM Integration

The service posts to the CRM using `multipart/form-data` with the exact field names the CRM expects:

| Your DTO field | CRM field |
|----------------|-----------|
| `OfficialPhone` | `phone` |
| `Dept` | `department` |
| `VoiceMessageIds` → binary files | `voice_file[]` |
| `ImageMessageIds` → binary files | `images[]` |

Auth header: `access-token: {Crm:ApiKey}`

The CRM returns:
```json
{
  "status": "success",
  "data": { "id": 13, "attachments": [...] }
}
```

`data.id` is stored as `CrmTicketId` in `WhatsAppComplaints`.

---

## Environment Variables (Production)

| Key | Description |
|-----|-------------|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Crm__SubmitUrl` | CRM complaint endpoint |
| `Crm__ApiKey` | CRM access token |

---

## License

Internal use — PRAN-RFL Group.
