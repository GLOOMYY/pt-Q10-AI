# Escenarios · Entradas inválidas (`ESC-ENT`)

Validación del cuerpo de `POST /api/asistente/preguntar`: preguntas vacías, longitud, JSON mal formado e identidades inválidas.

- **400**: el cuerpo no cumple el contrato (§5). Se rechaza antes del LLM, con `tokens` y `costoUsd` en 0.
- **403**: el cuerpo es válido, pero la identidad no (R-SEG-04, D-08).

Los valores `<generar: …>` los construye el ejecutor de pruebas según las reglas deterministas de README §3.3.

## Vacías y longitud

---

### ESC-ENT-01 · Pregunta vacía

- **Verifica:** §5 Respuestas de error, R-LOG-01, CL-22, AC-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "",
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
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

---

### ESC-ENT-02 · Pregunta solo con espacios

- **Verifica:** CL-22
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "     ",
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
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

---

### ESC-ENT-03 · Exactamente 1 000 caracteres

- **Verifica:** CL-22, D-12
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "<generar: '¿Cuál es la nota mínima para aprobar una asignatura?' + ' Gracias.' repetido hasta completar exactamente 1000 caracteres>",
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
  "status": 200
}
```

---

### ESC-ENT-04 · 1 001 caracteres

- **Verifica:** CL-22, D-12
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "<generar: el texto de ESC-ENT-03 + 'X' (1001 caracteres)>",
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
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

**Notas:** Se rechaza antes del LLM: tokens y costo en 0.

---

### ESC-ENT-05 · 1 000 caracteres más espacios alrededor

- **Verifica:** CL-22
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "<generar: '   ' + texto de ESC-ENT-03 + '   '>",
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
  "status": 200
}
```

**Notas:** El límite se mide después de quitar los espacios del inicio y del final.

---

### ESC-ENT-06 · 1 000 caracteres con tildes y ñ

- **Verifica:** CL-22
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "<generar: 'ñáéíóú' repetido hasta 1000 caracteres>",
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
  "status": 200
}
```

**Notas:** Se cuentan caracteres, no bytes: en UTF-8 este texto ocupa más de 1 000 bytes y aun así es válido.

## Cuerpo mal formado (400)

---

### ESC-ENT-07 · JSON inválido

- **Verifica:** §5
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": "{\"pregunta\": \"hola\", \"usuario\": "
}
```

**Output esperado**

```json
{
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

**Notas:** `cuerpo` va como texto: se envía tal cual.

---

### ESC-ENT-08 · Falta `pregunta`

- **Verifica:** §5
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
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
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

---

### ESC-ENT-09 · Falta `usuario`

- **Verifica:** §5
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio?"
  }
}
```

**Output esperado**

```json
{
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

---

### ESC-ENT-10 · `rol` no permitido

- **Verifica:** §5
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio?",
    "usuario": {
      "rol": "profesor",
      "id": "EST-001"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

---

### ESC-ENT-11 · `pregunta` no es texto

- **Verifica:** §5
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": 123,
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
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

---

### ESC-ENT-12 · Cuerpo vacío

- **Verifica:** §5
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {}
}
```

**Output esperado**

```json
{
  "status": 400,
  "respuesta": {
    "igual": "La solicitud no es válida."
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false,
  "tokens": 0,
  "costoUsd": 0,
  "log": {
    "estudiantesConsultados": [],
    "status": 400
  }
}
```

## Identidad inválida (403)

---

### ESC-ENT-13 · ID inexistente

- **Verifica:** R-SEG-04, CL-12, D-08
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-999"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 403,
  "respuesta": {
    "igual": "No estás autorizado a obtener esa información."
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 22"
      }
    ]
  },
  "usoDatosEstudiante": false,
  "log": {
    "estudiantesConsultados": [],
    "status": 403
  },
  "tokens": 0,
  "costoUsd": 0
}
```

---

### ESC-ENT-14 · Rol y ID incoherentes

- **Verifica:** R-SEG-04, CL-12
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio?",
    "usuario": {
      "rol": "estudiante",
      "id": "COORD-01"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 403,
  "respuesta": {
    "igual": "No estás autorizado a obtener esa información."
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 22"
      }
    ]
  },
  "usoDatosEstudiante": false,
  "log": {
    "estudiantesConsultados": [],
    "status": 403
  },
  "tokens": 0,
  "costoUsd": 0
}
```

---

### ESC-ENT-15 · ID con `%` mal codificado

- **Verifica:** API-01, R-SEG-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-%E0%A4%A"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 403,
  "respuesta": {
    "igual": "No estás autorizado a obtener esa información."
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 22"
      }
    ]
  },
  "usoDatosEstudiante": false,
  "log": {
    "estudiantesConsultados": [],
    "status": 403
  },
  "tokens": 0,
  "costoUsd": 0,
  "despues": [
    {
      "metodo": "GET",
      "ruta": "/api/salud",
      "status": 200
    }
  ]
}
```

**Notas:** La API simulada no debe recibir la petición (si la recibiera, se caería). Después, el sistema sigue respondiendo.

---

### ESC-ENT-16 · ID con recorrido de rutas

- **Verifica:** API-01, R-SEG-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio?",
    "usuario": {
      "rol": "estudiante",
      "id": "../programas"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 403,
  "respuesta": {
    "igual": "No estás autorizado a obtener esa información."
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 22"
      }
    ]
  },
  "usoDatosEstudiante": false,
  "log": {
    "estudiantesConsultados": [],
    "status": 403
  },
  "tokens": 0,
  "costoUsd": 0
}
```

---

### ESC-ENT-17 · ID vacío

- **Verifica:** R-SEG-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio?",
    "usuario": {
      "rol": "estudiante",
      "id": ""
    }
  }
}
```

**Output esperado**

```json
{
  "status": 403,
  "respuesta": {
    "igual": "No estás autorizado a obtener esa información."
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 22"
      }
    ]
  },
  "usoDatosEstudiante": false,
  "log": {
    "estudiantesConsultados": [],
    "status": 403
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** `usuario` existe pero su ID tiene formato inválido: 403, igual que cualquier otra identidad inválida (§5 solo da 400 si falta `usuario`; D-26).

## Normalización (200)

---

### ESC-ENT-18 · ID en minúsculas y con espacios

- **Verifica:** API-05
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio ponderado?",
    "usuario": {
      "rol": "estudiante",
      "id": " est-001 "
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
      "4.39"
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

**Notas:** Se normaliza a `EST-001` antes de validar.
