# Escenarios · Simulación de notas (`ESC-SIM`)

Preguntas del tipo "¿cuánto necesito en el corte 3 para…?". Verifican R-CAL-11 y D-15: la simulación la calcula el backend, considera el redondeo de la definitiva y se presenta como proyección.

**Regla de cálculo** (R-CAL-11): con `acumulada = Σ(peso × corte calificado)` y `pesoPendiente = Σ pesos de los cortes sin nota`, se busca la menor nota `x` (con 1 decimal, la misma en todos los cortes pendientes) que cumpla `acumulada + pesoPendiente × x ≥ meta − 0.05`, de modo que la definitiva **redondeada** alcance la meta.

**Función unitaria** (nombre canónico para las pruebas): `CalcularNotaNecesaria(corte1, corte2, corte3, meta)`. Los cortes sin nota llegan como `null`. Devuelve un `decimal` con 1 decimal, o uno de estos resultados especiales: `"inalcanzable"` (x > 5.0), `"asegurada"` (x ≤ 0.0) o `"sin_cortes_pendientes"`.

---

## Nivel unitario

### ESC-SIM-01 · Trampa de redondeo: aprobar Estadística (EST-006)

- **Verifica:** R-CAL-11, CL-28, D-15
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularNotaNecesaria",
  "argumentos": { "corte1": 3.9, "corte2": 4.1, "corte3": null, "meta": 3.0 }
}
```

**Output esperado**

```json
{ "resultado": 1.4 }
```

**Notas:** 2.40 + 0.4·1.4 = 2.96 → se redondea a 3.0 y aprueba. Con 1.3: 2.92 → 2.9 y no aprueba. La respuesta ingenua (sin considerar el redondeo) sería 1.5.

---

### ESC-SIM-02 · Meta de la beca (4.3)

- **Verifica:** R-CAL-11
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularNotaNecesaria",
  "argumentos": { "corte1": 3.9, "corte2": 4.1, "corte3": null, "meta": 4.3 }
}
```

**Output esperado**

```json
{ "resultado": 4.7 }
```

**Notas:** 2.40 + 0.4·4.7 = 4.28 → 4.3. Con 4.6: 4.24 → 4.2.

---

### ESC-SIM-03 · Meta inalcanzable

- **Verifica:** R-CAL-11
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularNotaNecesaria",
  "argumentos": { "corte1": 3.9, "corte2": 4.1, "corte3": null, "meta": 5.0 }
}
```

**Output esperado**

```json
{ "resultado": "inalcanzable" }
```

**Notas:** se necesitaría x ≥ 6.375, fuera de la escala 0.0–5.0.

---

### ESC-SIM-04 · Meta ya asegurada

- **Verifica:** R-CAL-11
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularNotaNecesaria",
  "argumentos": { "corte1": 5.0, "corte2": 5.0, "corte3": null, "meta": 3.0 }
}
```

**Output esperado**

```json
{ "resultado": "asegurada" }
```

**Notas:** con 0.0 en el corte 3 la definitiva ya es 3.0.

---

### ESC-SIM-05 · Límite exacto con dos cortes pendientes

- **Verifica:** R-CAL-02, R-CAL-11, CL-07
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularNotaNecesaria",
  "argumentos": { "corte1": 4.0, "corte2": null, "corte3": null, "meta": 3.0 }
}
```

**Output esperado**

```json
{ "resultado": 2.5 }
```

**Notas:** 1.20 + 0.7·2.5 = 2.95 exacto → 3.0. Cae justo en la mitad: con `double`, (2.95 − 1.20) / 0.7 = 2.5000000000000004 y, al subir a 1 decimal, devuelve 2.6. Obliga a usar `decimal` (D-06).

---

### ESC-SIM-06 · Límite exacto con un corte pendiente

- **Verifica:** R-CAL-11
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularNotaNecesaria",
  "argumentos": { "corte1": 3.0, "corte2": 3.0, "corte3": null, "meta": 3.0 }
}
```

**Output esperado**

```json
{ "resultado": 2.9 }
```

**Notas:** 1.80 + 0.4·2.9 = 2.96 → 3.0. Con 2.8: 2.92 → 2.9. La respuesta ingenua sería 3.0.

---

### ESC-SIM-07 · Sin cortes pendientes

- **Verifica:** R-CAL-11
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularNotaNecesaria",
  "argumentos": { "corte1": 2.5, "corte2": 2.8, "corte3": 2.6, "meta": 3.0 }
}
```

**Output esperado**

```json
{ "resultado": "sin_cortes_pendientes" }
```

---

## Nivel api

### ESC-SIM-08 · Estudiante simula su propia asignatura (EST-006)

- **Verifica:** R-CAL-11, R-SEG-02, R-RES-02, R-LOG-02, CL-28, D-15, AC-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto necesito en el corte 3 de Estadística para pasar?",
    "usuario": { "rol": "estudiante", "id": "EST-006" }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": { "contiene": ["1.4", "2.40", "proyección"], "noContiene": ["1.5"] },
  "citas": {
    "incluye": [
      { "documento": "Datos del estudiante", "seccion": "Notas · periodo 2026-2" },
      { "documento": "evaluacion_y_calificaciones.md", "seccion": "Sección 2" },
      { "documento": "evaluacion_y_calificaciones.md", "seccion": "Sección 3" }
    ]
  },
  "usoDatosEstudiante": true,
  "tokens": ">0",
  "log": { "estudiantesConsultados": ["EST-006"], "status": 200, "noContiene": ["1.4", "2.40"] }
}
```

---

### ESC-SIM-09 · Meta inalcanzable (EST-006)

- **Verifica:** R-CAL-11
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto necesito en el corte 3 de Estadística para sacar 5.0 de definitiva?",
    "usuario": { "rol": "estudiante", "id": "EST-006" }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": { "noContiene": ["6.4", "6.375"] },
  "citas": {
    "incluye": [
      { "documento": "evaluacion_y_calificaciones.md", "seccion": "Sección 2" },
      { "documento": "evaluacion_y_calificaciones.md", "seccion": "Sección 3" }
    ]
  },
  "usoDatosEstudiante": true,
  "log": { "estudiantesConsultados": ["EST-006"], "status": 200 }
}
```

**Notas:** debe decir que la meta no es alcanzable. Nunca propone una nota fuera de la escala 0.0–5.0. No se usa `contiene: ["no"]`: es tan genérico que aparece en cualquier respuesta.

---

### ESC-SIM-10 · Asignatura perdida por inasistencia (EST-002)

- **Verifica:** R-CAL-06, R-CAL-11, R-HAB-02, CL-29
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué nota necesito para pasar Bases de Datos?",
    "usuario": { "rol": "estudiante", "id": "EST-002" }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": { "contiene": ["0.0", "inasistencia"] },
  "citas": {
    "incluye": [
      { "documento": "reglamento_academico_2025.md", "seccion": "Artículo 11" },
      { "documento": "Datos del estudiante", "seccion": "Asistencia · periodo 2026-2" }
    ]
  },
  "usoDatosEstudiante": true,
  "log": { "estudiantesConsultados": ["EST-002"], "status": 200 }
}
```

**Notas:** 9 de 48 faltas (18.75 %) → perdida con 0.0 y no habilitable (Art. 7). Ninguna nota la salva. El 3.8 de los cortes no debe presentarse como su definitiva, pero no se pone en `noContiene`: el corte 1 vale 3.8 y la respuesta puede mencionarlo legítimamente como aclaración (igual que ESC-EST-01).

---

### ESC-SIM-11 · Asignatura que ya tiene definitiva (EST-002)

- **Verifica:** R-CAL-11, R-HAB-01, CL-29
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto necesito en el corte 3 de Cálculo Integral para pasar?",
    "usuario": { "rol": "estudiante", "id": "EST-002" }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": { "contiene": ["2.6", "habilit"] },
  "citas": {
    "incluye": [
      { "documento": "reglamento_academico_2025.md", "seccion": "Artículo 6" }
    ]
  },
  "usoDatosEstudiante": true,
  "log": { "estudiantesConsultados": ["EST-002"], "status": 200 }
}
```

**Notas:** los tres cortes ya están calificados (definitiva 2.6), así que no hay nada que simular. Como 2.6 ≥ 2.0 y no la perdió por faltas, puede habilitar (máximo 3.0).

---

### ESC-SIM-12 · Estudiante simula sobre otro estudiante

- **Verifica:** R-SEG-05, R-SEG-08, R-SEG-09, AC-02, D-13, R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto necesita Mateo Vargas en el corte 3 de Estadística para pasar?",
    "usuario": { "rol": "estudiante", "id": "EST-004" }
  }
}
```

**Output esperado**

```json
{
  "status": 403,
  "respuesta": { "igual": "No estás autorizado a obtener esa información." },
  "citas": {
    "exactas": [
      { "documento": "reglamento_academico_2025.md", "seccion": "Artículo 22" }
    ]
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": { "estudiantesConsultados": [], "status": 403, "motivo": "barrera determinista" }
}
```

**Notas:** EST-004 y EST-006 son del mismo programa; aun así, 403. Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-SIM-13 · Coordinador simula un estudiante de su programa

- **Verifica:** R-CAL-11, R-SEG-03, R-RES-02
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto necesita EST-006 en el corte 3 de Estadística para aprobar?",
    "usuario": { "rol": "coordinador", "id": "COORD-02" }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": { "contiene": ["1.4"], "noContiene": ["1.5"] },
  "citas": {
    "incluye": [
      { "documento": "Datos del programa", "seccion": "ADM · periodo 2026-2" },
      { "documento": "evaluacion_y_calificaciones.md", "seccion": "Sección 2" },
      { "documento": "evaluacion_y_calificaciones.md", "seccion": "Sección 3" }
    ]
  },
  "usoDatosEstudiante": true,
  "log": { "estudiantesConsultados": ["EST-006"], "status": 200 }
}
```

---

### ESC-SIM-14 · Coordinador simula un estudiante de otro programa

- **Verifica:** R-SEG-03, CL-10, AC-02, R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto necesita EST-006 en el corte 3 de Estadística para aprobar?",
    "usuario": { "rol": "coordinador", "id": "COORD-01" }
  }
}
```

**Output esperado**

```json
{
  "status": 403,
  "respuesta": { "igual": "No estás autorizado a obtener esa información." },
  "citas": {
    "exactas": [
      { "documento": "reglamento_academico_2025.md", "seccion": "Artículo 22" }
    ]
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": { "estudiantesConsultados": [], "status": 403, "motivo": "barrera determinista" }
}
```

**Notas:** la autorización se resuelve con las listas de estudiantes de ING-SIS, sin consultar a EST-006 (R-SEG-03). Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.
