# Izmenjuj.si – brezplačno testno gostovanje

Za razvojni javni test je priporočena kombinacija **Render Free + Supabase Free**.

## 1. Supabase

1. Ustvari nov Supabase projekt.
2. V Storage ustvari **public bucket** z imenom `izmenjuj-images`.
3. V Connect / Database kopiraj PostgreSQL connection string za .NET/Npgsql.
4. V Project Settings / API Keys kopiraj strežniški **secret key**. Tega ključa nikoli ne zapisuj v GitHub.
5. Kopiraj tudi Project URL, npr. `https://xxxxx.supabase.co`.

Aplikacija uporablja PostgreSQL neposredno prek Entity Framework Core. Ob prvem zagonu ustvari svoje tabele v `public` shemi, če še ne obstajajo.

## 2. Render

Repo vsebuje `Dockerfile` in `render.yaml`, zato ga lahko Render zgradi neposredno iz GitHuba.

1. V Renderju izberi **New > Blueprint**.
2. Poveži GitHub in izberi repozitorij `Darksideno1/Izmenjuj`.
3. Render prebere `render.yaml` in ustvari Free Web Service.
4. Ob ustvarjanju vnesi zahtevane skrivne okoljske spremenljivke:

- `ConnectionStrings__DefaultConnection` – Supabase PostgreSQL connection string
- `Storage__SupabaseUrl` – Supabase Project URL
- `Storage__SupabaseSecretKey` – Supabase server-side secret key

Nastavljeni sta tudi:

- `Storage__Provider=Supabase`
- `Storage__Bucket=izmenjuj-images`

## Pomembno

- Render Free se po obdobju neaktivnosti uspava, zato je prvi obisk po premoru počasnejši.
- Ne uporabljaj lokalnega `wwwroot/uploads` za javno testno stran, ker je brezplačni Render datotečni sistem začasen.
- Nobenega gesla, connection stringa ali Supabase secret ključa ne dodajaj v GitHub.
- Brezplačno gostovanje je namenjeno razvoju in testiranju, ne končni produkciji.

## Lokalni razvoj

Lokalno je privzeto `Storage:Provider=Local`, zato se slike shranjujejo v `wwwroot/uploads`. PostgreSQL lahko zaženeš z `docker compose up -d`.
