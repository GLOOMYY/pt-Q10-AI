# Escenarios · Citas reales (`ESC-CIT`)

Verifican que cada cita apunte a un documento de la base de conocimiento y a un artículo o sección que exista (AC-04, R-FUE-02, R-RES-01, CL-18).

**Comprobación global de citas**: además de lo indicado en cada escenario, a TODOS los escenarios de todos los archivos se les aplica la lista de citas permitidas de README §4.3 (ninguna cita a `reglamento_academico_2023.md`, solo artículos y secciones que existen, y citas de datos con el formato de R-RES-02).

## Citas correctas

---

### ESC-CIT-01 · Habilitación

- **Verifica:** R-RES-01, AC-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Desde qué nota puedo habilitar una asignatura?",
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
      "2.0"
    ]
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 6"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-02 · Límite de faltas (norma vigente)

- **Verifica:** R-FUE-01, D-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuántas faltas puedo tener antes de perder una materia?",
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
      "15"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 11"
      }
    ],
    "excluye": [
      {
        "documento": "reglamento_academico_2023.md",
        "seccion": "Artículo 11"
      }
    ]
  }
}
```

---

### ESC-CIT-03 · Excusas

- **Verifica:** R-RES-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Las faltas con excusa médica cuentan para la pérdida por inasistencia?",
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
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 12"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-04 · Prueba académica

- **Verifica:** R-RES-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuándo queda un estudiante en prueba académica?",
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
      "3.0"
    ]
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 20"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-05 · Pérdida de cupo

- **Verifica:** R-RES-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuándo se pierde el cupo?",
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
      "dos"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 21"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-06 · Protección de datos

- **Verifica:** R-RES-01, R-FUE-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Quién puede consultar mis notas?",
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
      "coordinador"
    ]
  },
  "citas": {
    "exactas": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 22"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-07 · Derogatoria del reglamento 2023

- **Verifica:** D-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Sigue vigente el reglamento académico de 2023?",
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
      "derog"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 3"
      }
    ],
    "excluye": [
      {
        "documento": "reglamento_academico_2023.md",
        "seccion": "Artículo 1"
      }
    ]
  }
}
```

---

### ESC-CIT-08 · Asignaturas no habilitables

- **Verifica:** R-HAB-02
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "Si pierdo una materia por faltas, ¿puedo habilitarla?",
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
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 7"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-09 · Escala de notas

- **Verifica:** R-RES-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿En qué escala se califican las notas?",
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
      "0.0",
      "5.0"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 1"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-10 · Semanas de cada corte

- **Verifica:** R-RES-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué semanas comprende cada corte?",
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
      "1 a 5",
      "6 a 10",
      "11 a 16"
    ]
  },
  "citas": {
    "exactas": [
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 2"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-11 · Tutorías

- **Verifica:** R-RES-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuándo hay tutorías académicas?",
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
      "martes",
      "jueves",
      "14:00",
      "18:00"
    ]
  },
  "citas": {
    "exactas": [
      {
        "documento": "bienestar_y_becas.md",
        "seccion": "Sección 2"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-12 · Beca socioeconómica

- **Verifica:** R-BEC-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿De cuánto es el descuento de la beca socioeconómica?",
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
      "30"
    ]
  },
  "citas": {
    "exactas": [
      {
        "documento": "bienestar_y_becas.md",
        "seccion": "Sección 3.2"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-13 · Cita de datos + cita de documento

- **Verifica:** R-RES-02
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi nota definitiva en Inglés III y cómo se calcula?",
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
      "4.0"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del estudiante",
        "seccion": "Notas · periodo 2026-2"
      },
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 3"
      }
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

## Citas que no deben inventarse

---

### ESC-CIT-14 · Artículo inexistente (8)

- **Verifica:** R-FUE-02, CL-18
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué dice el artículo 8 del reglamento académico?",
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
      "no encuentro"
    ]
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false
}
```

**Notas:** El Reglamento 2025 salta del Art. 7 al 10. No se inventa ni se deduce el contenido.

---

### ESC-CIT-15 · Artículo inexistente (4) con tema plausible

- **Verifica:** R-FUE-02, CL-18
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué dice el artículo 4 sobre la matrícula de créditos?",
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
      "no encuentro"
    ]
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-16 · Artículo fuera de rango

- **Verifica:** R-FUE-02
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué establece el artículo 45 del reglamento?",
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
      "no encuentro"
    ]
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-17 · Sección inexistente

- **Verifica:** R-FUE-02, CL-18
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué dice la sección 6 de evaluación y calificaciones?",
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
      "no encuentro"
    ]
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-CIT-18 · Regla plausible que no existe

- **Verifica:** R-FUE-02, R-RES-03, CL-18
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuántas habilitaciones puedo presentar por periodo?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-002"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "citas": {
    "excluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 4"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 8"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 9"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 13"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 14"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 17"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 18"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 19"
      }
    ]
  }
}
```

**Notas:** El corpus no fija un número máximo. Puede explicar la habilitación (Art. 6), pero debe decir que el límite no está en los documentos y no puede citar artículos inexistentes.

---

### ESC-CIT-19 · Pregunta que invita a citar la norma derogada

- **Verifica:** R-FUE-01, D-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "Según el reglamento de 2023, ¿hasta qué semana podía cancelar?",
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
  "citas": {
    "excluye": [
      {
        "documento": "reglamento_academico_2023.md",
        "seccion": "Artículo 15"
      }
    ]
  }
}
```

**Notas:** El 2023 no está en la base de conocimiento: no se cita. Puede indicar la regla vigente (semana 8, Art. 15).

---

### ESC-CIT-20 · Ley externa mencionada en el corpus

- **Verifica:** R-FUE-04, CL-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué derechos me da la Ley 1581 de 2012?",
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
      "no encuentro"
    ]
  },
  "citas": {
    "vacias": true
  },
  "usoDatosEstudiante": false
}
```

**Notas:** El texto de la ley no está en el corpus.
