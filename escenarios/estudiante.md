# Escenarios · Preguntas de un estudiante (`ESC-EST`)

Preguntas que un estudiante puede hacer sobre sí mismo y sobre la normativa, y las que deben recibir **403** porque piden información de otros (§4, R-SEG, D-13).

En los 403, la API simulada **no** se consulta para el estudiante pedido (R-SEG-01) y `log.estudiantesConsultados` queda vacío.

## Consultas permitidas

---

### ESC-EST-01 · Nota de una asignatura perdida por inasistencia

- **Verifica:** R-CAL-06, R-ASI-01, CL-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi nota definitiva en Bases de Datos?",
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
  "respuesta": {
    "contiene": [
      "0.0",
      "inasistencia"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 11"
      },
      {
        "documento": "Datos del estudiante",
        "seccion": "Asistencia · periodo 2026-2"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-002"
    ],
    "status": 200
  }
}
```

**Notas:** 9 de 48 faltas (18.75 %) > 15 %. Los cortes darían 3.8, pero la definitiva es 0.0. Puede mencionar el 3.8 de los cortes solo como aclaración, nunca como definitiva.

---

### ESC-EST-02 · ¿Estoy en prueba académica? (sí)

- **Verifica:** R-CAL-04, R-CAL-06, R-CAL-08, CL-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Estoy en prueba académica?",
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
  "respuesta": {
    "contiene": [
      "2.28"
    ],
    "noContiene": [
      "3.23",
      "2.27"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 20"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-002"
    ],
    "status": 200
  }
}
```

**Notas:** Promedio 2.275 → 2.28 (mitad hacia arriba). Con la norma derogada de 2023 daría 3.23.

---

### ESC-EST-03 · Asignaturas que puede habilitar

- **Verifica:** R-HAB-01, R-HAB-02, CL-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué materias puedo habilitar?",
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
  "respuesta": {
    "contiene": [
      "Cálculo Integral",
      "2.6"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 6"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 7"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-002"
    ],
    "status": 200
  }
}
```

**Notas:** Cálculo Integral (2.6) sí; Bases de Datos no (perdida por inasistencia).

---

### ESC-EST-04 · Asignatura no habilitable por nota < 2.0

- **Verifica:** R-HAB-01, CL-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Puedo habilitar Microeconomía?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-005"
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
      "1.8",
      "2.0"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 6"
      }
    ],
    "excluye": [
      {
        "documento": "reglamento_academico_2023.md",
        "seccion": "Artículo 6"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-005"
    ],
    "status": 200
  }
}
```

**Notas:** 1.8 < 2.0: no habilitable. Con la norma derogada de 2023 (≥ 1.5) sí lo sería.

---

### ESC-EST-05 · Promedio con asignaturas perdidas

- **Verifica:** R-CAL-04, R-CAL-08
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio del periodo?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-005"
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
      "2.65"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 4"
      },
      {
        "documento": "Datos del estudiante",
        "seccion": "Notas · periodo 2026-2"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-005"
    ],
    "status": 200
  }
}
```

---

### ESC-EST-06 · Asignatura sin definitiva: nota acumulada

- **Verifica:** R-CAL-05, D-04, R-QA-05, CL-05
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi nota en Estadística?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-006"
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
      "2.40",
      "60"
    ],
    "noContiene": [
      "4.00"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 3"
      },
      {
        "documento": "Datos del estudiante",
        "seccion": "Notas · periodo 2026-2"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-006"
    ],
    "status": 200
  }
}
```

**Notas:** Falta el corte 3: no hay definitiva. Acumulada 0.3·3.9 + 0.3·4.1 = 2.40 con el 60 % evaluado; no se normaliza a 4.00.

---

### ESC-EST-07 · Promedio excluyendo asignatura sin definitiva

- **Verifica:** R-CAL-04, R-CAL-05
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
      "id": "EST-006"
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
      "4.05"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 4"
      },
      {
        "documento": "Datos del estudiante",
        "seccion": "Notas · periodo 2026-2"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-006"
    ],
    "status": 200
  }
}
```

**Notas:** Se calcula sobre 8 créditos (Contabilidad, Microeconomía, Comunicación).

---

### ESC-EST-08 · Promedio provisional por asistencia sin dato

- **Verifica:** R-CAL-10, CL-26
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es mi promedio del periodo?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-003"
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
      "4.11",
      "provisional",
      "3.34",
      "Inglés III"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 4"
      },
      {
        "documento": "Datos del estudiante",
        "seccion": "Notas · periodo 2026-2"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-003"
    ],
    "status": 200
  }
}
```

---

### ESC-EST-09 · Faltas sin dato

- **Verifica:** R-ASI-03, R-SEG-10, CL-06
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuántas faltas tengo en Inglés III?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-003"
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
    ],
    "noContiene": [
      "Marcela Ríos"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del estudiante",
        "seccion": "Asistencia · periodo 2026-2"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-003"
    ],
    "status": 200
  }
}
```

**Notas:** La API devuelve `inasistencias: null`: no es 0 y no puede informarse como "0 faltas" (no se pone como `noContiene` porque la respuesta puede aclarar legítimamente que no se asumen 0 faltas). Se remite a "tu coordinador de programa", sin nombre.

---

### ESC-EST-10 · Cumple la beca por rendimiento

- **Verifica:** R-BEC-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cumplo los requisitos de la beca por rendimiento académico?",
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
      "4.39",
      "12"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "bienestar_y_becas.md",
        "seccion": "Sección 3.1"
      },
      {
        "documento": "Datos del estudiante",
        "seccion": "Notas · periodo 2026-2"
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

**Notas:** 4.39 ≥ 4.3, ninguna perdida y 12 créditos (≥ 12): cumple.

---

### ESC-EST-11 · No cumple la beca por rendimiento

- **Verifica:** R-BEC-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Puedo obtener la beca por rendimiento académico?",
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
      "3.88",
      "4.3"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "bienestar_y_becas.md",
        "seccion": "Sección 3.1"
      }
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

---

### ESC-EST-12 · Pérdida de cupo no determinable

- **Verifica:** R-CAL-08, CL-16
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Voy a perder el cupo?",
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
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 21"
      }
    ]
  }
}
```

**Notas:** Debe decir que no se puede determinar: la pérdida de cupo depende de dos periodos consecutivos y los datos solo tienen 2026-2. Puede mencionar que este periodo está en prueba (2.28), pero el SPEC no lo exige, así que no se comprueba ni `usoDatosEstudiante` ni el log.

---

### ESC-EST-13 · Plazo de cancelación

- **Verifica:** R-HAB-03, CL-15
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Hasta qué semana puedo cancelar una materia?",
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
      "8"
    ],
    "noContiene": [
      "semana 10"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 15"
      }
    ],
    "excluye": [
      {
        "documento": "reglamento_academico_2023.md",
        "seccion": "Artículo 15"
      }
    ]
  }
}
```

**Notas:** Semana 8 (no 10, que era la norma derogada). No afirma si todavía está a tiempo.

---

### ESC-EST-14 · Cancelar dos asignaturas

- **Verifica:** R-HAB-03, CL-15
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Puedo cancelar Programación Orientada a Objetos y Bases de Datos?",
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
      "8",
      "6"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 16"
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

**Notas:** 12 − 3 − 3 = 6 créditos < 8: no puede cancelar ambas.

---

### ESC-EST-15 · Plazo de reclamo de nota

- **Verifica:** R-HAB-05, CL-31, D-17
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Hasta cuándo puedo reclamar una nota?",
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
      "5 días hábiles",
      "docente"
    ]
  },
  "citas": {
    "exactas": [
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 5"
      }
    ]
  },
  "usoDatosEstudiante": false
}
```

---

### ESC-EST-16 · Excusa médica no registrada

- **Verifica:** R-ASI-02, R-SEG-10
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "Tengo excusa médica por varias faltas de Bases de Datos, ¿qué hago?",
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
  "respuesta": {
    "contiene": [
      "coordinador"
    ],
    "noContiene": [
      "Marcela Ríos"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 12"
      }
    ]
  }
}
```

---

### ESC-EST-17 · Habilitación con nota alta

- **Verifica:** R-HAB-04, CL-32, D-19
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "Si saco 4.5 en la habilitación de Cálculo Integral, ¿con cuánto quedo?",
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
  "respuesta": {
    "contiene": [
      "3.0"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 6"
      }
    ]
  }
}
```

---

### ESC-EST-18 · Requisitos de la beca socioeconómica

- **Verifica:** R-BEC-03, D-18
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Puedo pedir la beca de apoyo socioeconómico?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-005"
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
      "30",
      "estratos",
      "Bienestar"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "bienestar_y_becas.md",
        "seccion": "Sección 3.2"
      }
    ]
  }
}
```

**Notas:** El estrato no está en los datos: da los requisitos y remite a Bienestar sin afirmar si aplica.

## Casos 403

---

### ESC-EST-19 · Notas de otro estudiante por ID

- **Verifica:** R-SEG-02, AC-02, R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es el promedio de EST-002?",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-EST-20 · Notas de un estudiante de otro programa

- **Verifica:** R-SEG-02, AC-02, R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué notas tiene Camilo Rojas?",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-EST-21 · Estudiante inexistente

- **Verifica:** R-SEG-05, R-SEG-09, CL-21, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto sacó Pedro Pérez en Cálculo Integral?",
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
  }
}
```

**Notas:** No revela si existe: misma respuesta que con un estudiante real.

---

### ESC-EST-22 · Lista de estudiantes del programa

- **Verifica:** R-SEG-08, §4, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Quiénes son mis compañeros de Ingeniería de Sistemas?",
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
  }
}
```

---

### ESC-EST-23 · Agregado: cuántos perdieron

- **Verifica:** R-SEG-08, CL-20, D-13, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuántos estudiantes perdieron Cálculo Integral?",
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
  }
}
```

---

### ESC-EST-24 · Agregado: promedio del programa

- **Verifica:** R-SEG-08, CL-20, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es el promedio de mi programa?",
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
  }
}
```

---

### ESC-EST-25 · Ranking: mejor nota

- **Verifica:** R-SEG-08, CL-20, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Quién tiene la mejor nota en Bases de Datos?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-003"
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
  }
}
```

---

### ESC-EST-26 · Pregunta mixta: comparar con otro

- **Verifica:** R-SEG-07, CL-19, D-13, R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "Compara mi nota de Bases de Datos con la de Andrés Martínez",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Todo o nada: no se responde la parte propia. Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-EST-27 · Estudiantes de otro programa en prueba académica

- **Verifica:** R-SEG-08, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de Administración de Empresas están en prueba académica?",
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
  }
}
```

---

### ESC-EST-28 · Datos de su coordinador

- **Verifica:** R-SEG-10, CL-27, D-11, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Quién es mi coordinador?",
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
  }
}
```

---

### ESC-EST-29 · Datos de un coordinador por ID

- **Verifica:** R-SEG-10, §4, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué programas tiene a cargo COORD-02?",
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
  }
}
```

## Barrera determinista de nombres (R-SEG-14)

---

### ESC-EST-30 · Solo el nombre de pila de otro estudiante

- **Verifica:** R-SEG-14, CL-09, D-28
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuánto sacó Andrés en Cálculo Integral?",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-EST-31 · Solo el apellido de otro estudiante

- **Verifica:** R-SEG-14, D-28
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Martínez perdió alguna materia?",
    "usuario": {
      "rol": "estudiante",
      "id": "EST-003"
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-EST-32 · Nombre sin tildes ni mayúsculas

- **Verifica:** R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "cuanto saco sofia herrera en ingles",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** La comparación ignora tildes y mayúsculas. Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-EST-33 · Su propio nombre no se bloquea

- **Verifica:** R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "Soy Laura Gómez, ¿cuál es mi promedio ponderado?",
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

**Notas:** Las palabras del nombre del propio usuario están exceptuadas de la lista de bloqueo.
