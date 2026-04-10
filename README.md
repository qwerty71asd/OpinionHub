# OpinionHub (учебный проект)

## Запуск

1) Запусти PostgreSQL.
2) Создай БД (если нужно):

   - через psql: `create database opinionhub;`

3) Проверь строку подключения в `OpinionHub.Web/appsettings.json` (DefaultConnection).
4) Накати миграции:

```powershell
cd .\OpinionHub.Web
# если dotnet-ef не установлен:
dotnet tool install --global dotnet-ef

dotnet ef database update
```

5) Запусти:

```powershell
cd .\OpinionHub.Web
_dotnet run
```

Откроется сайт (обычно https://localhost:7xxx).

## Админка

Админка доступна по адресу: `/Admin`.

### Вариант 1 (самый простой): сидинг админа в Development

В `Development` при старте приложения автоматически создаётся пользователь **admin** и ему выдаётся роль **Admin**.
Настройки лежат в `OpinionHub.Web/appsettings.Development.json`:

- login: `admin`
- password: `Admin12345`

Пароль можно поменять там же.

### Вариант 2: повысить существующего пользователя до Admin через PostgreSQL (psql + PowerShell)

Если ты уже зарегистрировал пользователя (например, `SUPERPOCIK`) и хочешь просто выдать ему роль Admin:

```powershell
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"

# ВАЖНО: лучше подключаться тем же пользователем БД, который у тебя в строке подключения
# (например opinionhub_user). Пароль возьми из DefaultConnection.
$env:PGPASSWORD = "<пароль_пользователя_БД>"

$sql = @"
insert into public.""AspNetUserRoles"" (""UserId"",""RoleId"")
select u.""Id"", r.""Id""
from public.""AspNetUsers"" u
join public.""AspNetRoles"" r on r.""NormalizedName""='ADMIN'
where u.""NormalizedUserName""='SUPERPOCIK'
and not exists (
  select 1
  from public.""AspNetUserRoles"" ur
  where ur.""UserId""=u.""Id"" and ur.""RoleId""=r.""Id""
);
"@

[System.IO.File]::WriteAllText(".\make_admin.sql", $sql, (New-Object System.Text.UTF8Encoding($false)))
& $psql -U opinionhub_user -h localhost -p 5432 -d opinionhub -f .\make_admin.sql

Remove-Item Env:PGPASSWORD
```

После этого логинишься на сайте обычным паролем, который задавал при регистрации, и заходишь в `/Admin`.

