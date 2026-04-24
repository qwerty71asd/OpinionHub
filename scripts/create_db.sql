\set ON_ERROR_STOP on

-- 1) Роль/пользователь
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'opinionhub_user') THEN
    CREATE ROLE opinionhub_user LOGIN PASSWORD 'opinionhub_password';
  END IF;
END
$$;

-- 2) База (через psql \gexec, так можно условно создать)
SELECT 'CREATE DATABASE opinionhub OWNER opinionhub_user'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'opinionhub')
\gexec

-- 3) Подключаемся к созданной БД
\connect opinionhub

-- 4) Права
GRANT ALL PRIVILEGES ON DATABASE opinionhub TO opinionhub_user;
GRANT USAGE, CREATE ON SCHEMA public TO opinionhub_user;

ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO opinionhub_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO opinionhub_user;