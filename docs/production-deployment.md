# RinconV2: datos persistentes de producción

La publicación de la aplicación es reemplazable. Los datos persistentes deben vivir fuera de cada release.

## Directorios previstos en Linux

- Aplicación: `/var/www/rinconv2/current`
- Fotos de productos: `/var/lib/rinconv2/uploads/products`
- Claves de Data Protection: `/var/lib/rinconv2/keys`
- Backups: `/var/backups/rinconv2`

## Variables obligatorias

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://127.0.0.1:5006`
- `AllowedHosts=v2.rinconweb.online`
- `ConnectionStrings__ConexionPostgres=...`
- `DataProtection__KeysPath=/var/lib/rinconv2/keys`
- `Storage__ProductImagesPath=/var/lib/rinconv2/uploads/products`

Las credenciales de `BootstrapAdmin` se usan solamente para crear el primer administrador y deben retirarse del servicio después del primer inicio correcto.

## Base nueva y vacía

No se debe copiar la base local ni ejecutar los scripts de datos de prueba. En el VPS se crea una base PostgreSQL nueva y, antes de iniciar `rinconv2.service` por primera vez, se aplican todas las migraciones:

```powershell
dotnet ef database update --project Rincon.DataAccess --startup-project Rincon --configuration Release
```

El comando se ejecuta desde una copia temporal del repositorio con la cadena del VPS configurada mediante `ConnectionStrings__ConexionPostgres`. Después se publica solamente la salida de `dotnet publish`; el código fuente y el SDK no necesitan quedar en producción. La aplicación no aplica migraciones automáticamente para evitar cambios destructivos accidentales durante un reinicio.

Al finalizar, la base contendrá únicamente el esquema, roles y el administrador inicial. No contendrá productos, cuentas, ventas ni movimientos locales.

## Backup

`scripts/backup-postgres.sh` requiere `BACKUP_DIR`, `UPLOADS_DIR`, `DB_NAME`, `DB_USER` y `PGPASSWORD`. Genera y valida un dump PostgreSQL y un archivo comprimido de las fotos. Ambos forman una única unidad lógica de restauración: las fotos no deben excluirse del backup porque la base sólo conserva sus rutas.

Además de la retención local, debe mantenerse una copia fuera del VPS.
