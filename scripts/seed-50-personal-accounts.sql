-- Carga idempotente de 50 cuentas personales ficticias para RinconV2.
-- Datos exclusivamente destinados a desarrollo y pruebas.
-- Si un DNI ya existe, esa persona se omite.

BEGIN;

WITH people("Sequence", "FullName", "Address") AS (
    VALUES
        (1, 'Sofía Martínez', 'San Martín 124, Córdoba'),
        (2, 'Mateo González', 'Belgrano 856, Córdoba'),
        (3, 'Valentina Rodríguez', 'Rivadavia 432, Villa María'),
        (4, 'Benjamín Fernández', 'Sarmiento 215, Río Cuarto'),
        (5, 'Martina López', 'Mitre 987, Córdoba'),
        (6, 'Bautista García', 'Urquiza 641, Alta Gracia'),
        (7, 'Catalina Pérez', 'Colón 318, Córdoba'),
        (8, 'Felipe Sánchez', 'Alberdi 744, Jesús María'),
        (9, 'Emilia Romero', 'Maipú 159, Córdoba'),
        (10, 'Joaquín Díaz', 'Lavalle 523, Río Tercero'),
        (11, 'Isabella Torres', '9 de Julio 882, Córdoba'),
        (12, 'Santino Ruiz', 'General Paz 267, Carlos Paz'),
        (13, 'Renata Álvarez', 'Dean Funes 731, Córdoba'),
        (14, 'Lorenzo Gómez', 'Santa Fe 406, San Francisco'),
        (15, 'Delfina Acosta', 'Buenos Aires 194, Córdoba'),
        (16, 'Franco Medina', 'Independencia 658, Río Cuarto'),
        (17, 'Olivia Herrera', 'Chacabuco 325, Córdoba'),
        (18, 'Tomás Suárez', 'Caseros 917, Villa Allende'),
        (19, 'Josefina Castro', 'Ayacucho 248, Córdoba'),
        (20, 'Agustín Ortiz', 'España 570, Villa María'),
        (21, 'Malena Núñez', 'Corrientes 803, Córdoba'),
        (22, 'Bruno Molina', 'Salta 436, Alta Gracia'),
        (23, 'Julieta Silva', 'Entre Ríos 129, Córdoba'),
        (24, 'Thiago Rojas', 'Tucumán 765, Río Tercero'),
        (25, 'Mía Cabrera', 'Catamarca 354, Córdoba'),
        (26, 'Lautaro Vega', 'Mendoza 612, Carlos Paz'),
        (27, 'Victoria Navarro', 'La Rioja 281, Córdoba'),
        (28, 'Juan Cruz Aguirre', 'Jujuy 940, Jesús María'),
        (29, 'Camila Arias', 'Obispo Trejo 517, Córdoba'),
        (30, 'Nicolás Domínguez', 'Ituzaingó 208, Villa María'),
        (31, 'Paula Benítez', 'Duarte Quirós 683, Córdoba'),
        (32, 'Facundo Peralta', 'Vélez Sarsfield 371, Río Cuarto'),
        (33, 'Abril Sosa', 'Hipólito Yrigoyen 824, Córdoba'),
        (34, 'Ramiro Figueroa', 'Marcelo T. de Alvear 146, Alta Gracia'),
        (35, 'Luciana Ferreyra', '27 de Abril 592, Córdoba'),
        (36, 'Ignacio Quiroga', 'Arturo M. Bas 739, San Francisco'),
        (37, 'Florencia Bustos', 'Fragueiro 264, Córdoba'),
        (38, 'Gonzalo Ponce', 'Oncativo 875, Río Tercero'),
        (39, 'Milagros Luna', 'Cañada 413, Córdoba'),
        (40, 'Lucas Carrizo', 'Olmos 628, Villa Allende'),
        (41, 'Candela Miranda', 'Lima 307, Córdoba'),
        (42, 'Máximo Correa', 'Santa Rosa 951, Carlos Paz'),
        (43, 'Agustina Vargas', 'La Tablada 186, Córdoba'),
        (44, 'Simón Campos', 'Sucre 547, Jesús María'),
        (45, 'Pilar Godoy', 'Humberto Primo 792, Córdoba'),
        (46, 'Pedro Maidana', 'Achával Rodríguez 239, Villa María'),
        (47, 'Alma Villalba', 'Pueyrredón 664, Córdoba'),
        (48, 'Salvador Leiva', 'Richieri 328, Río Cuarto'),
        (49, 'Clara Mansilla', 'Paraná 718, Córdoba'),
        (50, 'Manuel Farías', 'Avellaneda 455, Alta Gracia')
),
catalog AS (
    SELECT
        people."FullName",
        (80000000 + people."Sequence")::text AS "DNI",
        people."Address",
        '+54 9 351 555-' || LPAD(people."Sequence"::text, 4, '0') AS "Phone",
        LOCALTIMESTAMP - ((people."Sequence" % 30)::text || ' days')::interval AS "Date",
        TRUE AS "isActive"
    FROM people
),
inserted AS (
    INSERT INTO "PersonalAccounts"
        ("FullName", "DNI", "Address", "Phone", "Date", "isActive")
    SELECT
        catalog."FullName",
        catalog."DNI",
        catalog."Address",
        catalog."Phone",
        catalog."Date",
        catalog."isActive"
    FROM catalog
    WHERE NOT EXISTS (
        SELECT 1
        FROM "PersonalAccounts" existing
        WHERE existing."DNI" = catalog."DNI"
    )
    RETURNING "Id"
)
SELECT COUNT(*) AS "CuentasPersonalesInsertadas" FROM inserted;

COMMIT;

-- Verificación opcional:
-- SELECT COUNT(*) FROM "PersonalAccounts";
-- SELECT * FROM "PersonalAccounts" ORDER BY "Id" DESC LIMIT 20;
