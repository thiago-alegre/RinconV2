-- Carga idempotente de 200 productos para RinconV2 (PostgreSQL).
-- Si un nombre ya existe, ese producto se omite.

BEGIN;

WITH product_types("Name", "Description", "PurchasePrice", "SalePrice") AS (
    VALUES
        ('Remera básica', 'Remera de algodón clásica para uso diario.', 6500.00, 12000.00),
        ('Remera estampada', 'Remera de algodón con estampa frontal.', 7800.00, 14500.00),
        ('Remera oversize', 'Remera de corte amplio y calce cómodo.', 8500.00, 16000.00),
        ('Musculosa', 'Musculosa liviana de algodón.', 5200.00, 9800.00),
        ('Camisa manga corta', 'Camisa fresca de manga corta.', 11500.00, 22000.00),
        ('Camisa manga larga', 'Camisa clásica de manga larga.', 13500.00, 25500.00),
        ('Buzo cuello redondo', 'Buzo de frisa con cuello redondo.', 16500.00, 31000.00),
        ('Buzo con capucha', 'Buzo de frisa con capucha y bolsillo.', 19500.00, 36500.00),
        ('Campera deportiva', 'Campera liviana con cierre frontal.', 24000.00, 45000.00),
        ('Campera de abrigo', 'Campera térmica para bajas temperaturas.', 32000.00, 59000.00),
        ('Pantalón jogger', 'Pantalón jogger cómodo con cintura elastizada.', 14500.00, 27500.00),
        ('Pantalón de gabardina', 'Pantalón resistente de gabardina.', 18500.00, 35000.00),
        ('Jean clásico', 'Jean de corte clásico y tiro medio.', 21000.00, 39500.00),
        ('Calza deportiva', 'Calza elastizada para actividad física.', 10500.00, 19800.00),
        ('Short deportivo', 'Short liviano con cintura elastizada.', 8500.00, 16000.00),
        ('Bermuda de gabardina', 'Bermuda de gabardina con bolsillos.', 12500.00, 23500.00),
        ('Vestido casual', 'Vestido cómodo para uso diario.', 16500.00, 31500.00),
        ('Vestido de fiesta', 'Vestido elegante para ocasiones especiales.', 28500.00, 53000.00),
        ('Pollera corta', 'Pollera corta de tela elastizada.', 9800.00, 18500.00),
        ('Pollera larga', 'Pollera larga de caída liviana.', 12500.00, 23500.00),
        ('Pijama adulto', 'Conjunto de pijama suave de dos piezas.', 14500.00, 27000.00),
        ('Pijama infantil', 'Conjunto de pijama infantil de algodón.', 10500.00, 19800.00),
        ('Pantuflas', 'Pantuflas acolchadas con suela antideslizante.', 7200.00, 13500.00),
        ('Medias térmicas', 'Par de medias térmicas suaves.', 2800.00, 5500.00),
        ('Gorro de lana', 'Gorro tejido de abrigo.', 4200.00, 8200.00),
        ('Bufanda tejida', 'Bufanda larga tejida de textura suave.', 5200.00, 9900.00),
        ('Guantes térmicos', 'Guantes abrigados para uso urbano.', 4500.00, 8500.00),
        ('Manta polar individual', 'Manta polar liviana para una plaza.', 12000.00, 22500.00),
        ('Manta polar matrimonial', 'Manta polar amplia para dos plazas.', 17500.00, 33000.00),
        ('Manta tejida', 'Manta decorativa tejida para sillón o cama.', 19500.00, 36500.00),
        ('Acolchado individual', 'Acolchado relleno para cama individual.', 26000.00, 48000.00),
        ('Acolchado matrimonial', 'Acolchado relleno para cama matrimonial.', 36000.00, 66000.00),
        ('Juego de sábanas individual', 'Juego de sábanas de algodón para una plaza.', 15500.00, 29000.00),
        ('Juego de sábanas matrimonial', 'Juego de sábanas de algodón para dos plazas.', 22000.00, 41000.00),
        ('Funda de almohada', 'Funda de almohada suave y lavable.', 3500.00, 6800.00),
        ('Almohadón decorativo', 'Almohadón relleno para decoración.', 6800.00, 12800.00),
        ('Toallón de baño', 'Toallón absorbente de algodón.', 9500.00, 17800.00),
        ('Toalla de mano', 'Toalla compacta y absorbente.', 4200.00, 8000.00),
        ('Bata de baño', 'Bata suave con cinturón y bolsillos.', 18500.00, 34500.00),
        ('Bolso multiuso', 'Bolso amplio para compras, viaje o gimnasio.', 13500.00, 25500.00)
),
variants("Variant", "StockOffset") AS (
    VALUES
        ('Negro', 0),
        ('Blanco', 3),
        ('Gris', 6),
        ('Azul', 9),
        ('Rosa', 12)
),
catalog AS (
    SELECT
        product_types."Name" || ' - ' || variants."Variant" AS "Name",
        product_types."Description" || ' Color ' || lower(variants."Variant") || '.' AS "Description",
        product_types."PurchasePrice",
        product_types."SalePrice",
        5 + variants."StockOffset" + ((ROW_NUMBER() OVER ())::integer % 7) AS "Quantity"
    FROM product_types
    CROSS JOIN variants
),
inserted AS (
    INSERT INTO "Products"
        ("Name", "Description", "PurchasePrice", "SalePrice", "Quantity", "ImageUrl", "IsActive", "CreatedAt")
    SELECT
        catalog."Name",
        catalog."Description",
        catalog."PurchasePrice",
        catalog."SalePrice",
        catalog."Quantity",
        NULL,
        TRUE,
        LOCALTIMESTAMP
    FROM catalog
    WHERE NOT EXISTS (
        SELECT 1
        FROM "Products" existing
        WHERE lower(existing."Name") = lower(catalog."Name")
    )
    RETURNING "Id"
)
SELECT COUNT(*) AS "ProductosInsertados" FROM inserted;

COMMIT;

-- Verificación opcional:
-- SELECT COUNT(*) FROM "Products";
-- SELECT * FROM "Products" ORDER BY "Id" DESC LIMIT 20;
