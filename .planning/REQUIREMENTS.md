# Requirements: AgroPlan Backend

**Defined:** 2026-11-09
**Core Value:** Producers can rely on secure, farm-scoped operational records and reviewable assistance from planting through harvest.

Los requisitos de alcance del piloto confirmado se separan de la cartera funcional mayor. La clasificación temporal para el roadmap debe respetar `.planning/intel/decisions.md` y el reporte de conflictos: primero el piloto; capacidades funcionales amplias, Mercado, transporte y extensiones en fases posteriores. Las frases de aceptación reproducen el nivel de detalle provisto por la fuente; no se agregan criterios no documentados.

## REQ-pilot-accounts
- source: ../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md
- description: Creación y gestión de cuentas de usuario.
- acceptance: El usuario puede crear y administrar su cuenta; el documento no especifica el flujo técnico ni criterios de validación adicionales.
- scope: cuentas

## REQ-pilot-farms-parcels
- source: ../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md
- description: Gestión de finca y parcelas.
- acceptance: El propietario o administrador puede crear/gestionar finca y parcelas; el modelo y permisos completos son propuestas a revisar.
- scope: finca, parcelas, permisos por finca

## REQ-pilot-crop-cycle
- source: ../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md
- description: Selección de cultivo y ciclo.
- acceptance: Se registra cultivo y ciclo asociados a la parcela; la fecha y catálogo detallados deben definirse en el diseño.
- scope: cultivos, ciclos

## REQ-pilot-sowing-plan
- source: ../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md
- description: Plan de siembra asistido por IA y revisión climática.
- acceptance: El sistema propone el plan con los datos y proveedor que se definan; el usuario revisa y confirma antes de guardarlo. Proveedor IA, reglas agronómicas y datos climáticos siguen pendientes.
- scope: planificación, IA, clima

## REQ-pilot-planting-frame
- source: ../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md
- description: Diseño del marco de cultivo asistido por IA.
- acceptance: La capacidad y cálculos verificables se basan en reglas; IA compara y explica opciones y el encargado elige/confirma. Los parámetros agronómicos siguen pendientes.
- scope: marco de cultivo, IA

## REQ-pilot-manual-logbook
- source: ../agro-plan-doc/Doc/AgroPlan - Alcance y Permisos del Piloto.md
- description: Bitácora con registro manual de actividades.
- acceptance: El usuario autorizado registra actividades en bitácora; correcciones preservan el historial en la propuesta técnica.
- scope: bitácora

## REQ-pilot-test-suite
- source: User-approved Phase 1 quality addition, 2026-11-10.
- description: Automated xUnit v3 coverage for the complete pilot API, domain rules, persistence constraints, worker flows, and security boundaries.
- acceptance: Unit and isolated integration tests cover every Phase 1 requirement, including current/revoked farm membership, safe missing-data behavior, immutable logbook/attachments, outbox and consumer idempotency, and recovery; tests need no production credentials.
- scope: testing, quality, security

## REQ-pilot-api-docs
- source: User-approved Phase 1 quality addition, 2026-11-10.
- description: Interactive Scalar API documentation backed by an OpenAPI document.
- acceptance: Development exposes a valid OpenAPI JSON document and Scalar UI; production routes remain disabled by default.
- scope: API, documentation

## REQ-pilot-provider-neutral-ai
- source: User-approved Phase 1 architecture addition, 2026-11-10.
- description: Keep AI proposal orchestration independent from the currently selected Gemini provider.
- acceptance: Application and worker use a provider-neutral AI contract; Gemini remains the current adapter and can be replaced through DI without changing application behavior.
- scope: AI, architecture

## REQ-rf-g-01
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Registrar fincas y parcelas con nombre, ubicación, tamaño y cultivo.
- acceptance: Los atributos descritos pueden registrarse y consultarse.
- scope: Gestión, fincas, parcelas

## REQ-rf-g-02
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Registrar manualmente gastos, jornales y materiales por día y cultivo.
- acceptance: Cada registro conserva día y cultivo asociados.
- scope: costos, jornales, materiales

## REQ-rf-g-03
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Calcular en tiempo real el costo de producción por quintal.
- acceptance: El cálculo se deriva de los datos de costos y producción; las reglas exactas están pendientes.
- scope: costos, producción

## REQ-rf-g-04
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Proveer calendario estándar de tareas por cultivo.
- acceptance: Se genera o consulta un calendario para el cultivo seleccionado.
- scope: calendario, tareas, cultivos

## REQ-rf-g-05
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mantener bitácora diaria de siembra, riego, fumigación y cosecha, con fotos opcionales.
- acceptance: El usuario registra las actividades y puede adjuntar fotos opcionales.
- scope: bitácora, archivos

## REQ-rf-g-06
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Gestionar hasta cinco trabajadores en funciones base.
- acceptance: Las cuentas de trabajadores quedan vinculadas a una finca.
- scope: trabajadores, membresías

## REQ-rf-g-07
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Bloquear la fecha de cosecha tras registrar químicos hasta cumplir el periodo de carencia.
- acceptance: El backend impide o señala cosecha anterior a la fecha segura calculada según el periodo de carencia.
- scope: aplicaciones, cosecha, seguridad

## REQ-rf-g-08
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Consultar precios de referencia.
- acceptance: Se muestra el dato con su fecha, unidad y fuente cuando está disponible.
- scope: precios

## REQ-rf-g-09
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Sincronizar automáticamente y exponer estado pendiente o sincronizado.
- acceptance: Reintentos no duplican efectos; se muestra el estado de la operación.
- scope: sincronización offline

## REQ-rf-g-10
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Proponer ventana de siembra según cultivo, variedad, zona, fecha deseada, agua, parámetros agronómicos y datos climáticos. Explicar factores, datos faltantes, escenarios, incertidumbre y vigencia.
- acceptance: La propuesta presenta factores, datos faltantes, escenarios, incertidumbre y vigencia; el proveedor, reglas y fuentes quedan pendientes.
- scope: planificación, clima, IA

## REQ-rf-g-11
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Calcular capacidad según área útil, pasillos y distancias entre plantas/surcos; comparar marcos validados y explicar densidad, acceso y riego; productor elige.
- acceptance: Cálculos verificables usan reglas; IA explica comparativas; productor confirma una opción. Distribución preliminar es posible si existen límites de parcela.
- scope: marco de cultivo, IA, parcelas

## REQ-rf-g-12
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Calcular rutas eficientes para tareas de siembra, fertilización, fumigación y cosecha y entregarlas a trabajadores asignados.
- acceptance: Ruta propuesta se revisa/confirma y puede descargarse para uso offline; proveedor geográfico y motor están por definir.
- scope: rutas, tareas, trabajadores

## REQ-rf-g-13
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Generar calendario del ciclo desde preparación hasta cosecha, actualizar avance con labores confirmadas y proponer cambios por ejecución o alertas vigentes de lluvia/viento.
- acceptance: IA explica cambios y el encargado confirma reprogramaciones.
- scope: ciclo, calendario, clima

## REQ-rf-g-14
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Permitir trabajadores ilimitados en el plan premium.
- acceptance: Límite de trabajadores se asocia al nivel de servicio configurado.
- scope: trabajadores, suscripciones

## REQ-rf-g-15
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Exportar reportes de costos y rendimiento.
- acceptance: El usuario autorizado puede descargar reportes de costos/rendimiento.
- scope: reportes, costos

## REQ-rf-g-16
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Acceder a la bolsa de alquiler de equipos de Fase 3.
- acceptance: Requisito futuro, sin comportamiento funcional detallado en el documento.
- scope: alquiler de equipos

## REQ-rf-g-17
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Vincular cuenta de trabajador a finca mediante creación o invitación del administrador.
- acceptance: Membresía se activa al aceptar la invitación según el modelo propuesto.
- scope: trabajadores, invitaciones, finca

## REQ-rf-g-18
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mostrar al trabajador tareas del día con estados pendiente, en curso, hecha y verificada.
- acceptance: El trabajador consulta solo tareas propias/asignadas.
- scope: tareas, autorización

## REQ-rf-g-19
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Proveer rutas recomendadas offline para tareas de campo.
- acceptance: Ruta descargada puede consultarse desconectado.
- scope: rutas, offline

## REQ-rf-g-20
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Adjuntar fotos y opcionalmente ubicación/hora como evidencia al completar tarea.
- acceptance: Evidencia se conserva localmente offline y se sube al recuperar señal.
- scope: evidencia, archivos, sincronización

## REQ-rf-g-21
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Administrador aprueba, rechaza con motivo o pide corrección de una tarea.
- acceptance: Solo tareas verificadas cuentan para jornal y nivel Con evidencia del pasaporte.
- scope: verificación, tareas, jornales

## REQ-rf-g-22
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Proveer al trabajador historial personal de tareas, evidencias y jornales verificados.
- acceptance: Los datos son privados salvo para el administrador de la finca.
- scope: trabajadores, historial

## REQ-rf-g-23
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Operar offline y conservar evidencia local hasta recuperar señal.
- acceptance: Las operaciones se sincronizan idempotentemente al reconectar.
- scope: offline, evidencia

## REQ-rf-g-24
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mantener accesibilidad con iconos, voz y WhatsApp.
- acceptance: Detalle visual y canales dependen del cliente y de servicios por definir.
- scope: accesibilidad, notificaciones

## REQ-rf-g-25
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Otorgar puntos por tareas completadas y verificadas, extra por evidencia completa y clara.
- acceptance: No se otorgan puntos solo por velocidad; trabajo debe verificarse.
- scope: gamificación

## REQ-rf-g-26
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Registrar rachas de días consecutivos con tareas verificadas.
- acceptance: Racha se actualiza a partir de tareas verificadas.
- scope: gamificación

## REQ-rf-g-27
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mantener niveles e insignias por constancia, calidad de evidencia y cosecha.
- acceptance: Criterios de otorgamiento configurados/documentados antes de activar.
- scope: gamificación

## REQ-rf-g-28
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Definir metas de equipo a nivel de finca con reconocimiento colectivo.
- acceptance: Progreso compartido se calcula respecto al valor objetivo definido.
- scope: gamificación, finca

## REQ-rf-g-29
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Permitir recompensas definidas por administrador, como bono, reconocimiento o prioridad; AgroPlan no entrega dinero.
- acceptance: El sistema registra recompensa, pero no ejecuta pago monetario.
- scope: gamificación, recompensas

## REQ-rf-g-30
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mostrar tablero de progreso; ranking entre trabajadores es opcional y activado por administrador.
- acceptance: Ranking permanece desactivado salvo configuración explícita por finca.
- scope: gamificación, privacidad

## REQ-rf-g-31
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Registrar riego con fecha, método (goteo, inundación, lluvia) y fuente de agua.
- acceptance: Los campos quedan asociados a finca/parcela/ciclo según contexto.
- scope: riego, bitácora

## REQ-rf-g-32
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Registrar aplicaciones de plaguicidas, fungicidas, fertilizantes y otros insumos, incluso con dron, y alertar frecuencia y periodo de carencia.
- acceptance: Aplicaciones estructuradas permiten calcular restricciones de cosecha.
- scope: insumos, carencia, cosecha

## REQ-rf-g-33
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Ofrecer guía orientativa de fertilización por etapa, recordatorios y aviso de que no sustituye al técnico.
- acceptance: Guía se presenta como orientativa.
- scope: fertilización, etapas

## REQ-rf-g-34
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Reforzar recordatorios en primeras semanas/etapas críticas del cultivo.
- acceptance: Calendario aplica recordatorios según parámetros de cultivo configurados.
- scope: alertas, cultivo

## REQ-rf-g-35
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Alertar lluvia, viento fuerte, sequía y temporada ciclónica, incluido el evaluador de siembra.
- acceptance: Las alertas muestran vigencia y fuente; proveedor de clima pendiente.
- scope: clima, alertas

## REQ-rf-g-36
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Exportar reporte automático de cultivo, área, fecha de siembra y cantidad cosechada.
- acceptance: Reporte disponible para extensionistas y asociaciones según autorización.
- scope: producción, reportes

## REQ-rf-g-37
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Dar a asesor/agente de área acceso a datos de fincas y reportes con autorización del productor.
- acceptance: Permiso limitado por finca, alcance y vigencia; puede revocarse.
- scope: asesor, autorización, reportes

## REQ-rf-g-38
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Proveer catálogo inicial con marco, etapas, duración de ciclo y fechas típicas de siembra, comenzando por cultivos del piloto.
- acceptance: Cada parámetro proviene de catálogo/documentación agronómica pendiente de validar.
- scope: cultivos, datos maestros

## REQ-rf-g-39
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Transcribir voz del productor/trabajador y crear borrador editable de actividad, fecha, parcela, cultivo, cantidad y unidad; señalar campos faltantes.
- acceptance: El usuario revisa y confirma antes de guardar la bitácora; audio se elimina al confirmar o siete días después del procesamiento sin revisión.
- scope: voz, IA, bitácora, archivos

## REQ-rf-g-40
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Redactar resumen semanal desde registros confirmados, tareas pendientes y datos faltantes; totales se calculan con datos estructurados y permiten consultar fuentes.
- acceptance: Encargado puede revisar y autorizar compartir con asesor.
- scope: IA, resumen, asesor

## REQ-rf-g-41
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Vincular registro confirmado a parcela/tarea, identificar tareas afectadas por cambios de fecha/etapa/ejecución y explicar calendario propuesto.
- acceptance: Encargado confirma cambios; tareas sujetas a verificación siguen flujo de aprobación.
- scope: bitácora, calendario, IA

## REQ-rf-g-42
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mostrar ventana estimada de cosecha y lista de acciones según siembra, parámetros y avance; registrar fecha/cantidad real y restricciones de carencia.
- acceptance: Productor confirma fecha y cantidad; backend aplica restricción de cosecha segura.
- scope: cosecha, calendario, IA

## REQ-rf-g-43
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: En fase futura, preparar reporte de síntomas por foto; agrónomo confirma diagnóstico y tratamiento.
- acceptance: No se automatiza diagnóstico/tratamiento sin confirmación del agrónomo; alcance se define tras validación.
- scope: IA, imágenes, asesor

## REQ-rf-m-01
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Publicar lote con cultivo, cantidad, calidad, ubicación, fotos y fecha disponible.
- acceptance: Solo vendedor autorizado publica; reglas de Mercado se definirán en Fase 2.
- scope: Mercado, lotes

## REQ-rf-m-02
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Elegir modalidad entre subasta de lote completo, venta directa fija o venta por fracciones negociadas.
- acceptance: Un lote no opera a la vez en subasta y venta fraccionada.
- scope: Mercado, venta

## REQ-rf-m-03
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Compradores ofertan y vendedor elige la oferta conveniente, no necesariamente la más alta.
- acceptance: Oferta queda vinculada a lote y decisión del vendedor.
- scope: Mercado, ofertas

## REQ-rf-m-04
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Subasta de lote completo con pujas públicas; negociación por fracciones es privada.
- acceptance: Visibilidad depende de modalidad elegida.
- scope: Mercado, negociación

## REQ-rf-m-05
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Permitir subastas destacadas con posicionamiento pagado.
- acceptance: Monetización y pagos siguen fuera del diseño inicial.
- scope: Mercado, promoción

## REQ-rf-m-06
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Buscar y filtrar lotes por cultivo, provincia, cantidad y precio.
- acceptance: Filtros aplican a publicaciones visibles.
- scope: Mercado, búsqueda

## REQ-rf-m-07
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mostrar precio de referencia al publicar y ofertar.
- acceptance: Precio conserva fuente, fecha, unidad y zona.
- scope: Mercado, precios

## REQ-rf-m-08
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Liberar contacto entre vendedor y comprador al aceptar oferta.
- acceptance: Contacto solo se comparte tras la condición documentada.
- scope: Mercado, privacidad

## REQ-rf-m-09
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Crear orden/acuerdo con cantidad, precio, fecha y lugar de entrega.
- acceptance: Ambas partes pueden consultar la constancia de trato.
- scope: Mercado, órdenes

## REQ-rf-m-10
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Confirmar entrega por ambas partes.
- acceptance: El cierre registra confirmaciones correspondientes.
- scope: Mercado, entrega

## REQ-rf-m-11
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Generar alerta de flete al cerrar una venta.
- acceptance: Solicitud de flete queda asociada a operación.
- scope: logística

## REQ-rf-m-12
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Permitir cotizaciones de transportistas verificados y selección por comprador o vendedor.
- acceptance: Solo transportistas verificados pueden cotizar.
- scope: logística, verificación

## REQ-rf-m-13
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Alertar transportistas de fletes cercanos según ubicación.
- acceptance: Geolocalización/fuente y permisos quedan pendientes.
- scope: logística, ubicación

## REQ-rf-m-14
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mostrar seguimiento básico del viaje.
- acceptance: Estados de recogida, tránsito y entrega se registran.
- scope: logística

## REQ-rf-m-15
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Generar código QR para acompañar la carga cuando se concreta venta.
- acceptance: QR refiere a pasaporte público con datos permitidos.
- scope: trazabilidad, QR

## REQ-rf-m-16
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mostrar origen, finca/provincia, fechas de siembra/cosecha, insumos y nivel de verificación al escanear QR.
- acceptance: No expone datos personales ni financieros; fuente respalda los datos publicados.
- scope: trazabilidad, privacidad

## REQ-rf-m-17
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Exponer niveles de verificación del pasaporte sin afirmar ausencia absoluta de químicos.
- acceptance: Nivel describe evidencia disponible y no certifica “libre de químicos” sin respaldo.
- scope: trazabilidad, verificación

## REQ-rf-m-18
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Permitir calificación de una a cinco estrellas entre partes después de entrega, con comentario opcional.
- acceptance: Calificación asociada a operación completada.
- scope: reputación

## REQ-rf-m-19
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Proveer perfil público con promedio, operaciones e insignias de verificación.
- acceptance: Solo datos definidos como públicos se muestran.
- scope: perfil, reputación

## REQ-rf-m-20
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Publicar preventa de cosecha futura con cantidad estimada y fecha prevista.
- acceptance: Estimaciones se distinguen de cantidades/fechas confirmadas tras cosecha.
- scope: Mercado, preventa

## REQ-rf-m-21
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Invitar compradores habituales y ofrecer lotes privadamente antes de abrirlos al público.
- acceptance: Publicación privada solo es visible a compradores invitados.
- scope: Mercado, contactos

## REQ-rf-m-22
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Indicar estado de productos con proceso posterior, como arroz en campo, listo o en factoría, y lugar de entrega.
- acceptance: Estado y lugar se conservan en publicación/orden.
- scope: Mercado, entrega

## REQ-rf-m-23
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Subastar lote completo, gana una oferta, y permitir publicar parte o toda la producción.
- acceptance: Una sola oferta gana el lote completo.
- scope: Mercado, subastas

## REQ-rf-m-24
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Vender por fracciones con cantidad mínima y precio negociado en privado por fracción.
- acceptance: Cantidad mínima y fracciones no exceden disponible.
- scope: Mercado, fracciones

## REQ-rf-m-25
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Comprador propone cantidad/precio y vendedor acepta, rechaza o contraoferta; cada acuerdo crea orden propia.
- acceptance: La cantidad disponible se actualiza por acuerdo.
- scope: Mercado, negociación

## REQ-rf-m-26
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Impedir acuerdos abiertos que superen cantidad disponible y cerrar propuestas sobrantes.
- acceptance: Reserva de cantidades es atómica y nunca rebasa disponibilidad.
- scope: Mercado, concurrencia

## REQ-rf-m-27
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Impedir modalidad simultánea de subasta y venta por fracciones para un lote.
- acceptance: Se conserva exactamente una modalidad de venta por lote.
- scope: Mercado, reglas de dominio

## REQ-rf-m-28
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Preparar con IA borrador de publicación desde plan o registro real, distinguiendo estimados de confirmados; vendedor revisa y decide.
- acceptance: No se publica sin decisión explícita del vendedor.
- scope: Mercado, IA

## REQ-rf-t-01
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Interfaz con iconos grandes, texto mínimo y lenguaje sencillo.
- acceptance: Diseño compatible con uso en campo; detalle queda al cliente.
- scope: transversal, accesibilidad

## REQ-rf-t-02
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Asistencia por voz para leer pantallas clave y registrar datos dictados.
- acceptance: Funciones requieren conexión cuando dependan de IA; entrada manual sigue disponible.
- scope: transversal, voz

## REQ-rf-t-03
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Enviar avisos importantes por WhatsApp y SMS además de notificaciones de la app.
- acceptance: Proveedores y políticas de envío están por definir.
- scope: transversal, notificaciones

## REQ-rf-t-04
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Mantener flujos de registro cortos con ejemplos visuales.
- acceptance: Criterio de UX, su implementación está en frontend.
- scope: transversal, accesibilidad

## REQ-rf-t-05
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Funcionar en teléfonos de gama baja con bajo consumo de datos y almacenamiento.
- acceptance: Métricas cuantitativas no están especificadas.
- scope: transversal, rendimiento

## REQ-rf-t-06
- source: ../agro-plan-doc/Doc/AgroPlan - Documentacion Funcional.md
- description: Proveer tutoriales breves en app y material impreso para capacitación presencial.
- acceptance: Cliente/documentación; alcance de backend indirecto.
- scope: transversal, capacitación



## Traceability

Each documented requirement is assigned to exactly one roadmap phase. The six pilot requirements are Phase 1; broader capabilities follow in later phases.

| Requirement | Phase | Status |
|-------------|-------|--------|
| REQ-pilot-accounts | Phase 1: Farm Planning Pilot | Complete |
| REQ-pilot-farms-parcels | Phase 1: Farm Planning Pilot | Complete |
| REQ-pilot-crop-cycle | Phase 1: Farm Planning Pilot | Complete |
| REQ-pilot-sowing-plan | Phase 1: Farm Planning Pilot | Complete |
| REQ-pilot-planting-frame | Phase 1: Farm Planning Pilot | Complete (safe mode; numeric comparison gated on validated agronomic rules and parcel geometry) |
| REQ-pilot-manual-logbook | Phase 1: Farm Planning Pilot | Complete |
| REQ-rf-g-01 | Phase 1: Farm Planning Pilot | Complete |
| REQ-rf-g-05 | Phase 1: Farm Planning Pilot | Complete |
| REQ-rf-g-10 | Phase 1: Farm Planning Pilot | Complete |
| REQ-rf-g-11 | Phase 1: Farm Planning Pilot | Complete (safe mode; missing inputs block capacity/options until rules and geometry are validated) |
| REQ-rf-g-38 | Phase 1: Farm Planning Pilot | Complete |
| REQ-pilot-test-suite | Phase 1: Farm Planning Pilot | Complete (46 unit + 9 PostgreSQL/RabbitMQ integration tests pass) |
| REQ-pilot-api-docs | Phase 1: Farm Planning Pilot | Complete (Development OpenAPI/Scalar available; routes absent in Production) |
| REQ-pilot-provider-neutral-ai | Phase 1: Farm Planning Pilot | Complete (IAiProposalProvider contract; Gemini is the current adapter) |
| REQ-rf-g-02 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-03 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-04 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-07 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-08 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-13 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-15 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-31 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-32 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-33 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-34 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-35 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-36 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-37 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-42 | Phase 2: Farm Operations and Safety | Complete |
| REQ-rf-g-06 | Phase 3: Workforce and Offline Operations | Pending |
| REQ-rf-g-09 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-12 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-14 | Phase 3: Workforce and Offline Operations | Pending |
| REQ-rf-g-17 | Phase 3: Workforce and Offline Operations | Pending |
| REQ-rf-g-18 | Phase 3: Workforce and Offline Operations | Pending |
| REQ-rf-g-19 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-20 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-21 | Phase 3: Workforce and Offline Operations | Pending |
| REQ-rf-g-22 | Phase 3: Workforce and Offline Operations | Pending |
| REQ-rf-g-23 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-25 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-26 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-27 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-28 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-29 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-30 | Phase 3: Workforce and Offline Operations | Complete |
| REQ-rf-g-41 | Phase 3: Workforce and Offline Operations | Pending |
| REQ-rf-g-39 | Phase 4: Applied Farm Intelligence | Pending |
| REQ-rf-g-40 | Phase 4: Applied Farm Intelligence | Pending |
| REQ-rf-g-43 | Phase 4: Applied Farm Intelligence | Pending |
| REQ-rf-g-16 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-01 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-02 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-03 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-04 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-05 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-06 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-07 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-08 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-09 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-10 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-15 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-16 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-17 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-18 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-19 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-20 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-21 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-22 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-23 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-24 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-25 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-26 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-27 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-m-28 | Phase 5: Marketplace and Traceability | Pending |
| REQ-rf-g-24 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-m-11 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-m-12 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-m-13 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-m-14 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-t-01 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-t-02 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-t-03 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-t-04 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-t-05 | Phase 6: Transport and Field-Service Integrations | Pending |
| REQ-rf-t-06 | Phase 6: Transport and Field-Service Integrations | Pending |

**Coverage:**
- Documented requirements: 83 total
- Mapped to phases: 83
- Unmapped: 0 ✓

---
*Requirements defined: 2026-11-09*
*Last updated: 2026-11-09 after roadmap creation*
