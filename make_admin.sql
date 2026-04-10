create extension if not exists pgcrypto;

insert into public."AspNetRoles" ("Id","Name","NormalizedName","ConcurrencyStamp")
select gen_random_uuid()::text, 'Admin','ADMIN', gen_random_uuid()::text
where not exists (select 1 from public."AspNetRoles" where "NormalizedName"='ADMIN');

insert into public."AspNetUserRoles" ("UserId","RoleId")
select u."Id", r."Id"
from public."AspNetUsers" u
join public."AspNetRoles" r on r."NormalizedName"='ADMIN'
where u."NormalizedUserName"='SUPERPOCIK'
and not exists (
  select 1
  from public."AspNetUserRoles" ur
  where ur."UserId" = u."Id" and ur."RoleId" = r."Id"
);