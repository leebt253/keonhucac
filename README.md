# World Cup Betting (MVC + EF Core + SQLite)

Ung dung keo World Cup noi bo cho 7 user co san.

## Cong nghe

- ASP.NET Core MVC
- Entity Framework Core
- SQLite
- Bootstrap 5

Luu y: may hien tai co .NET SDK 10, vi vay project duoc scaffold voi `net10.0` de chay ngay.

## Tai khoan mac dinh

- Hai / 123456 (Admin)
- Hung / 123456
- Nam / 123456
- Quy / 123456
- Trung / 123456
- Truong / 123456
- Viet / 123456

## Chay local

```powershell
dotnet run --project .\WorldCupBetting.Web\WorldCupBetting.Web.csproj
```

App mac dinh: http://localhost:5290

## Chuc nang chinh

- Dang nhap, dang xuat, doi mat khau
- Bang tran dau (desktop table, mobile card)
- Bat keo theo doi A/doi B, khoa keo theo gio UTC+7
- Tinh tien: Thang +10, Thua -20, Khong chon -20
- Tong ket tien theo user trong bang keo
- Du doan cua moi nguoi
- Bang xep hang vong bang
- Nhánh knock-out va tu dong day doi di tiep
- Admin CRUD tran dau, import JSON, quan ly doi, reset mat khau user, xuat Excel/PDF, tinh lai du lieu

## Import JSON

Mau file: `WorldCupBetting.Web/fixtures.sample.json`
