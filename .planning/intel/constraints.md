# Especificaciones y restricciones técnicas

## Arquitectura de servicios para el piloto
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: protocol
- content: Backend .NET con API ASP.NET Core y Worker .NET como servicios Docker separados desplegados en Render. Supabase PostgreSQL es la fuente de verdad y Supabase Storage conserva binarios; CloudAMQP RabbitMQ transporta trabajos y eventos; SignalR notifica a clientes conectados. ASP.NET Core Identity administra cuentas y contraseñas. No se incluye Redis en el piloto.

## Responsabilidades de API, worker y proveedores
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: protocol
- content: La API aplica autenticación y autorización por finca, reglas de negocio, sincronización offline, permisos temporales para archivos, acceso a datos y publicación de trabajos. El Worker procesa transcripción, borradores de bitácora, resúmenes y reportes; los resultados de IA requieren revisión del usuario antes de confirmarse. RabbitMQ transporta referencias a archivos, no contenido binario. PostgreSQL mantiene estado y datos estructurados.

## Trabajos asíncronos y notificación
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: protocol
- content: La API registra el trabajo y publica una referencia; el Worker actualiza resultado/estado en PostgreSQL y puede solicitar procesamiento al proveedor IA, que sigue por definir. El Worker publica eventos de finalización en RabbitMQ, la API consume esos eventos y notifica mediante SignalR. PostgreSQL sigue siendo fuente de verdad; los clientes consultan la API al reconectar.

## Outbox y entrega idempotente
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: protocol
- content: El cambio de negocio y el evento pendiente se guardan en la misma transacción PostgreSQL. Un publicador envía eventos a RabbitMQ y los marca publicados tras confirmación del broker. Puede haber entrega duplicada si falla tras la confirmación; cada evento usa ID estable y consumidores idempotentes. Estados propuestos: pendiente, publicado y error. Reintentos, concurrencia y tratamiento de errores siguen pendientes.

## Contrato de sincronización offline
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: protocol
- content: Cada cambio offline lleva UUID generado por cliente; backend guarda una clave única y resultado en la misma transacción para que los reintentos sean idempotentes. Los cambios se envían en orden de creación y respetan dependencias; operaciones independientes pueden continuar si falla una dependencia ajena. Las ediciones concurrentes se detectan mediante versión y devuelven conflicto sin sobrescritura silenciosa. Las entradas de bitácora son inmutables; una corrección crea una nueva operación que referencia la anterior. El servidor revalida permisos al sincronizar.

## Archivos y retención de audio
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: protocol
- content: El cliente solicita autorización temporal a la API, sube directamente a Supabase Storage y notifica a la API; esta verifica el objeto antes de marcarlo disponible. PostgreSQL guarda metadatos y referencias, nunca URLs temporales permanentes ni bytes. El audio de bitácora se elimina al confirmar el borrador o a los siete días de procesarlo si sigue sin revisar. La limpieza de cargas abandonadas requiere proceso programado; plazo pendiente.

## Identidad y permisos por finca
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: nfr
- content: ASP.NET Core Identity conserva tablas predeterminadas en inglés dentro del esquema identidad; tablas propias de AgroPlan se nombran en español. La API aplica autorización por usuario, finca y recurso en toda operación. Roles de finca incluyen propietario, administrador, trabajador y asesor autorizado; el acceso de asesor requiere consentimiento del propietario con alcance y vigencia. El soporte no accede automáticamente a datos privados.

## Acceso desconectado
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: nfr
- content: La primera entrada a un dispositivo requiere conexión. Después del inicio de sesión con conexión se pueden consultar y registrar datos locales aunque venza la sesión del servidor. Al reconectar, el usuario se autentica de nuevo si es necesario antes de sincronizar. La revocación de permisos no llega al dispositivo desconectado; el servidor valida permisos al sincronizar.

## Modelo relacional unificado y entidades futuras
- source: ../agro-plan-doc/Doc/AgroPlan - Diagrama Relacional Unificado.md
- type: schema
- content: El diagrama es un modelo de trabajo, no migración SQL. Propone Identity en identidad; datos de dominio en gestion; eventos pendientes, operaciones y conflictos de sincronización en infraestructura. Incluye fincas, membresías, parcelas, cultivos, ciclos, bitácora, tareas, archivos, trabajos, permisos, invitaciones, planes, asesoría, precios, borradores IA, auditoría, costos distribuidos y unidades. Gamificación, jornales, reportes y otras entidades del modelo unificado no se consideran incluidas automáticamente en el piloto. Migraciones deben resolver FKs entre esquemas, índices, nulabilidad, restricciones, permisos y retención.

## Reglas de dominio explícitas
- source: ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md
- type: nfr
- content: La capacidad del marco, costos, periodos de carencia y cantidades se calculan mediante reglas verificables; la IA explica o propone. Los borradores IA no modifican bitácora ni calendario hasta aprobación. El administrador confirma planes y cambios de calendario. El backend no concede acceso de finca solo por tener cuenta.

## Privacidad y observabilidad
- source: ../agro-plan-doc/Doc/AgroPlan - Arquitectura Backend.md
- type: nfr
- content: API y Worker usan ILogger con salida JSON estructurada en consola y Render recopila logs. Los logs incluyen fecha, nivel, servicio y trace ID propagado por RabbitMQ; no incluyen contraseñas, tokens, documentos de identidad, audio ni contenido completo de bitácora. La auditoría de acciones de negocio se guarda en PostgreSQL separada de logs operativos.

## Recuperación de cuentas
- source: ../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md
- type: protocol
- content: La recuperación inicia por solicitud del usuario, envío de código al correo, revisión por soporte y habilitación de cambio temporal si se aprueba; nunca se comunica la contraseña a soporte. Códigos no se registran en logs ni se almacenan en texto plano. Canal, expiración, intentos, espera, criterios de revisión, casos sin acceso al correo y revocación de sesiones permanecen pendientes.

## Modelo de datos del piloto y flujos de IA
- source: ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md
- type: schema
- content: El modelo conceptual cubre fincas, membresías, parcelas, cultivos y ciclos, bitácora, tareas, archivos, trabajos asíncronos, operaciones y conflictos de sincronización, borradores de IA y eventos pendientes. Los nombres de dominio se expresan en español, sin tildes ni guiones, mientras ASP.NET Core Identity conserva sus nombres en inglés. El documento no fija aún tipos SQL, índices ni migraciones.

## Procesamiento de bitácora por voz
- source: ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md
- type: protocol
- content: La API valida usuario y finca, registra el archivo pendiente y emite autorización temporal; el cliente carga directamente a Storage y solicita procesamiento. El Worker obtiene el audio, solicita transcripción y extracción al proveedor de IA, guarda un borrador revisable y actualiza el trabajo. El usuario confirma el borrador antes de crear una entrada de bitácora. El audio se elimina al confirmar o a los siete días de procesarlo si no se revisa; falta definir el mecanismo programado de limpieza.

## Sincronización, idempotencia y conflictos
- source: ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md
- type: protocol
- content: Cada operación local tiene identificador único, versión base cuando aplique y dependencias. El cliente envía cambios en orden de creación; si una dependencia falla, las dependientes esperan y las independientes pueden continuar. La API guarda efecto y resultado de forma atómica para devolver el mismo resultado ante un reintento. Los conflictos preservan ambas versiones; el usuario elige versión del servidor, versión local o corrección manual y la resolución vuelve a comprobar la versión vigente.

## Esquemas de PostgreSQL e integridad entre fincas
- source: ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md
- type: schema
- content: Se proponen los esquemas identidad, gestion e infraestructura en Supabase PostgreSQL. Separar esquemas no sustituye la autorización por finca. Las relaciones deben impedir vínculos entre entidades de fincas distintas; se deben definir claves compuestas, índices, nulabilidad, restricciones, migraciones entre esquemas y permisos de acceso antes de fijar el diseño físico.

## Reglas de fincas, parcelas y ciclos
- source: ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md
- type: schema
- content: Toda finca requiere provincia y municipio; GPS es opcional. La parcela conserva el valor y la unidad de superficie elegida. Para iniciar un ciclo se requieren parcela, cultivo, variedad y al menos una fecha de inicio planificada o real; se distinguen fechas estimadas y reales. Una parcela puede tener varios ciclos activos, incluidos cultivos asociados. El catálogo de unidades, conversiones, límites geográficos y reglas de área quedan pendientes.

## Reglas de cálculo, unidades y jornales
- source: ../agro-plan-doc/Doc/AgroPlan - Flujos de Datos y Modelo de Datos.md
- type: nfr
- content: La capacidad del marco, costos, periodos de carencia y cantidades se calculan con reglas verificables; la IA propone o explica resultados. Las conversiones deben estar validadas y conservar unidad original y magnitud. Un gasto compartido se distribuye entre ciclos sin exceder su total; un jornal verificado se suma una sola vez. La equivalencia del quintal, precisión, redondeo y reglas de ajuste siguen pendientes.

## Alcance del modelo relacional unificado
- source: ../agro-plan-doc/Doc/AgroPlan - Diagrama Relacional Unificado.md
- type: schema
- content: El diagrama es un modelo de trabajo, no SQL ni una migración lista. Incluye entidades previstas de tareas, trabajadores, aplicaciones de insumos, gastos, cosechas, archivos, asesoría, precios, borradores IA, auditoría, gamificación y costos distribuidos; varias exceden el piloto confirmado. Mercado y rutas quedan fuera del esquema inicial. Las claves Identity UUID, nulabilidad, relaciones, retención y restricciones requieren implementación explícita.

