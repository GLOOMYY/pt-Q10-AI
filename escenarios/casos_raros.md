# Escenarios · Casos raros (`ESC-RAR`)

Trampas encontradas en el material: redondeo exactamente en la mitad, límite exacto del 15 % de faltas y códigos de asignatura con formato de ID. Cubren los pendientes CL-02 y CL-17 del SPEC.

Otros casos raros ya cubiertos en otros archivos: datos `null` (ESC-EST-06, -08, -09), norma derogada (ESC-EST-02, -04, ESC-INY-14, ESC-CIT-02), caída de la API simulada con `%` (ESC-ENT-15), normalización de IDs (ESC-ENT-18) y simulación con redondeo (ESC-SIM-01, -05, -06).

**Funciones unitarias** (nombres canónicos para las pruebas):

- `CalcularDefinitiva(corte1, corte2, corte3)` → `decimal` con 1 decimal (R-CAL-01..03).
- `CalcularPromedio(asignaturas[{definitiva, creditos}])` → `decimal` con 2 decimales, o `"sin_promedio"` (R-CAL-04, R-CAL-12).
- `PierdePorInasistencia(inasistencias, clasesProgramadas)` → `true`, `false` o `"no_determinable"` (R-ASI-01, R-ASI-03).

## Redondeo en la mitad (unitario)

---

### ESC-RAR-01 · Definitiva en la mitad: 4.2 / 4.5 / 4.6 → 4.5

- **Verifica:** R-CAL-01..03, CL-07, CL-17, D-06
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularDefinitiva",
  "argumentos": {
    "corte1": 4.2,
    "corte2": 4.5,
    "corte3": 4.6
  }
}
```

**Output esperado**

```json
{
  "resultado": 4.5
}
```

**Notas:** EST-001 Cálculo Integral. Con `double` da 4.4499…; con `ToEven` da 4.4.

---

### ESC-RAR-02 · Definitiva en la mitad: 3.5 / 3.2 / 3.6 → 3.5

- **Verifica:** R-CAL-01..03, CL-07, CL-17, D-06
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularDefinitiva",
  "argumentos": {
    "corte1": 3.5,
    "corte2": 3.2,
    "corte3": 3.6
  }
}
```

**Output esperado**

```json
{
  "resultado": 3.5
}
```

**Notas:** EST-002 POO. Con `ToEven` da 3.4.

---

### ESC-RAR-03 · Definitiva en la mitad: 4.0 / 3.9 / 4.2 → 4.1

- **Verifica:** R-CAL-01..03, CL-07, CL-17, D-06
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularDefinitiva",
  "argumentos": {
    "corte1": 4.0,
    "corte2": 3.9,
    "corte3": 4.2
  }
}
```

**Output esperado**

```json
{
  "resultado": 4.1
}
```

**Notas:** EST-003 Cálculo Integral. Con `ToEven` da 4.0; con `double`, 4.0500…01.

---

### ESC-RAR-04 · Definitiva en la mitad: 4.1 / 4.0 / 4.3 → 4.2

- **Verifica:** R-CAL-01..03, CL-07, CL-17, D-06
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularDefinitiva",
  "argumentos": {
    "corte1": 4.1,
    "corte2": 4.0,
    "corte3": 4.3
  }
}
```

**Output esperado**

```json
{
  "resultado": 4.2
}
```

**Notas:** EST-006 Contabilidad. Con `double` da 4.1499… → 4.1.

---

### ESC-RAR-05 · Definitiva en la mitad: 3.8 / 3.5 / 3.9 → 3.8

- **Verifica:** R-CAL-01..03, CL-07, CL-17, D-06
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularDefinitiva",
  "argumentos": {
    "corte1": 3.8,
    "corte2": 3.5,
    "corte3": 3.9
  }
}
```

**Output esperado**

```json
{
  "resultado": 3.8
}
```

**Notas:** EST-005 Comunicación (3.75).

---

### ESC-RAR-06 · Definitiva en la mitad: 4.5 / 4.4 / 4.7 → 4.6

- **Verifica:** R-CAL-01..03, CL-07, CL-17, D-06
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularDefinitiva",
  "argumentos": {
    "corte1": 4.5,
    "corte2": 4.4,
    "corte3": 4.7
  }
}
```

**Output esperado**

```json
{
  "resultado": 4.6
}
```

**Notas:** EST-003 Inglés III (4.55).

---

### ESC-RAR-07 · Promedio en la mitad: 2.275 → 2.28

- **Verifica:** R-CAL-04, CL-07, CL-17
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularPromedio",
  "argumentos": {
    "asignaturas": [
      {
        "definitiva": 2.6,
        "creditos": 4
      },
      {
        "definitiva": 3.5,
        "creditos": 3
      },
      {
        "definitiva": 0.0,
        "creditos": 3
      },
      {
        "definitiva": 3.2,
        "creditos": 2
      }
    ]
  }
}
```

**Output esperado**

```json
{
  "resultado": 2.28
}
```

**Notas:** EST-002. Con `double`, 2.275 se representa como 2.27499… y `Math.Round` o `toFixed(2)` dan 2.27. Con `decimal` y `ToEven` sale 2.28 por coincidencia (8 es par), así que este caso detecta sobre todo el uso de `double`.

---

### ESC-RAR-08 · Promedio con definitivas ya redondeadas

- **Verifica:** R-CAL-04, D-03
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularPromedio",
  "argumentos": {
    "asignaturas": [
      {
        "definitiva": 4.5,
        "creditos": 4
      },
      {
        "definitiva": 4.8,
        "creditos": 3
      },
      {
        "definitiva": 4.1,
        "creditos": 3
      },
      {
        "definitiva": 4.0,
        "creditos": 2
      }
    ]
  }
}
```

**Output esperado**

```json
{
  "resultado": 4.39
}
```

**Notas:** EST-001 (pregunta de ejemplo 2). Con 4.4 en Cálculo daría 4.36.

---

### ESC-RAR-09 · Promedio sin asignaturas con definitiva

- **Verifica:** R-CAL-12, D-20
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "CalcularPromedio",
  "argumentos": {
    "asignaturas": []
  }
}
```

**Output esperado**

```json
{
  "resultado": "sin_promedio"
}
```

**Notas:** Nunca se divide entre 0 créditos.

## Límite exacto del 15 % (unitario)

---

### ESC-RAR-10 · 4/32 faltas (12.50 %) → no pierde

- **Verifica:** R-ASI-01, CL-02
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": 4,
    "clasesProgramadas": 32
  }
}
```

**Output esperado**

```json
{
  "resultado": false
}
```

---

### ESC-RAR-11 · 5/32 faltas (15.62 %) → pierde

- **Verifica:** R-ASI-01, CL-02
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": 5,
    "clasesProgramadas": 32
  }
}
```

**Output esperado**

```json
{
  "resultado": true
}
```

---

### ESC-RAR-12 · 7/48 faltas (14.58 %) → no pierde

- **Verifica:** R-ASI-01, CL-02
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": 7,
    "clasesProgramadas": 48
  }
}
```

**Output esperado**

```json
{
  "resultado": false
}
```

---

### ESC-RAR-13 · 8/48 faltas (16.67 %) → pierde

- **Verifica:** R-ASI-01, CL-02
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": 8,
    "clasesProgramadas": 48
  }
}
```

**Output esperado**

```json
{
  "resultado": true
}
```

---

### ESC-RAR-14 · 9/64 faltas (14.06 %) → no pierde

- **Verifica:** R-ASI-01, CL-02
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": 9,
    "clasesProgramadas": 64
  }
}
```

**Output esperado**

```json
{
  "resultado": false
}
```

---

### ESC-RAR-15 · 10/64 faltas (15.62 %) → pierde

- **Verifica:** R-ASI-01, CL-02
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": 10,
    "clasesProgramadas": 64
  }
}
```

**Output esperado**

```json
{
  "resultado": true
}
```

---

### ESC-RAR-16 · 9/48 faltas (18.75 %) → pierde

- **Verifica:** R-ASI-01, CL-02
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": 9,
    "clasesProgramadas": 48
  }
}
```

**Output esperado**

```json
{
  "resultado": true
}
```

---

### ESC-RAR-17 · Inasistencias sin dato

- **Verifica:** R-ASI-03, API-10
- **Nivel:** unitario
- **Precondiciones:** ninguna

**Input**

```json
{
  "funcion": "PierdePorInasistencia",
  "argumentos": {
    "inasistencias": null,
    "clasesProgramadas": 32
  }
}
```

**Output esperado**

```json
{
  "resultado": "no_determinable"
}
```

**Notas:** `null` no es 0.

## Colisión de códigos y formato (api)

---

### ESC-RAR-18 · `EST-103` es una asignatura, no un estudiante

- **Verifica:** CL-08
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cómo voy en EST-103?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-004"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": {
    "contiene": [
      "Estadística",
      "4.0"
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-004"
    ],
    "status": 200
  }
}
```

**Notas:** Pregunta legítima sobre su propia asignatura. Si se interpreta como un estudiante, daría un 403 equivocado.

---

### ESC-RAR-19 · `EST-103` para un estudiante que no la cursa

- **Verifica:** CL-08
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué nota tengo en EST-103?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-001"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": {
    "noContiene": [
      "Andrés Martínez",
      "Sofía Herrera",
      "Camilo Rojas",
      "Valentina Castro",
      "Mateo Vargas",
      "2.3",
      "2.40"
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-001"
    ],
    "status": 200
  }
}
```

**Notas:** EST-001 no cursa Estadística: debe decir que no tiene esa asignatura. Ni 403 ni datos de quien sí la cursa (EST-004: 4.0; EST-005: 2.3; EST-006: acumulada 2.40). No se pone 4.0 ni 3.9 en `noContiene` porque son notas propias de EST-001 (Inglés III) que pueden aparecer legítimamente.

---

### ESC-RAR-20 · `ING-204` es una asignatura, no un programa

- **Verifica:** CL-08
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi nota definitiva en ING-204?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-001"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "respuesta": {
    "contiene": [
      "Inglés III",
      "4.0"
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-001"
    ],
    "status": 200
  }
}
```

**Notas:** Además verifica API-06: la API devuelve `4` y debe mostrarse `4.0`.
