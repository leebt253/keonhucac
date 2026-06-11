# Deploy WorldCupBetting.Web to Render (Docker + SQLite)

## Cach nhanh nhat (Blueprint)

Repository da co san `render.yaml`.

1. Push code len GitHub/GitLab.
2. Vao Render -> New -> Blueprint.
3. Chon repository.
4. Render tu tao Web Service + Disk theo `render.yaml`.
5. Deploy va mo URL public de truy cap qua internet.

## Cach thu cong (neu khong dung Blueprint)

1. Render -> New -> Web Service.
2. Chon repository, Environment = Docker.
3. Tao Disk:
   - Mount path: `/var/data`
   - Size: 1GB tro len
4. Dat bien moi truong:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ConnectionStrings__DefaultConnection=Data Source=/var/data/worldcup.db`
5. Deploy.

## Ghi chu

- Dockerfile da cau hinh app chay cong `10000` cho Render.
- SQLite duoc giu ben vung nho Disk mount `/var/data`.
- Khong can CI/CD.
