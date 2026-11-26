# Escenarios · Preguntas de un coordinador (`ESC-COO`)

Preguntas que un coordinador puede hacer sobre los estudiantes de su programa, y las que deben recibir **403** porque piden información de otro programa o de otro coordinador (§4, R-SEG-03).

COORD-01 → ING-SIS (EST-001 a EST-003). COORD-02 → ADM (EST-004 a EST-006).

## Consultas permitidas

---

### ESC-COO-01 · Asignaturas perdidas en ING-SIS

- **Verifica:** R-SEG-03, R-CAL-06, R-LOG-02, CL-11, AC-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de mi programa tienen alguna asignatura perdida?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "Andrés Martínez",
      "Cálculo Integral",
      "2.6",
      "Bases de Datos",
      "inasistencia"
    ],
    "noContiene": [
      "Camilo Rojas",
      "Valentina Castro",
      "Mateo Vargas"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ING-SIS · periodo 2026-2"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 5"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 11"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-001",
      "EST-002",
      "EST-003"
    ],
    "status": 200,
    "noContiene": [
      "2.6",
      "Andrés"
    ]
  }
}
```

**Notas:** Debe mencionar que Sofía Herrera no tiene dato de asistencia en Inglés III (CL-11).

---

### ESC-COO-02 · Estudiantes en prueba académica (ING-SIS)

- **Verifica:** R-CAL-08, R-CAL-10
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de mi programa están en prueba académica?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "Andrés Martínez",
      "2.28"
    ],
    "noContiene": [
      "Camilo Rojas",
      "Valentina Castro",
      "Mateo Vargas"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ING-SIS · periodo 2026-2"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 20"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-001",
      "EST-002",
      "EST-003"
    ],
    "status": 200
  }
}
```

**Notas:** Sofía Herrera no está en prueba ni con su promedio provisional (4.11) ni en el peor caso (3.34).

---

### ESC-COO-03 · Estudiantes en prueba académica (ADM)

- **Verifica:** R-CAL-08
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de mi programa están en prueba académica?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-02"
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
      "Valentina Castro",
      "2.65"
    ],
    "noContiene": [
      "Laura Gómez",
      "Andrés Martínez",
      "Sofía Herrera"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ADM · periodo 2026-2"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 20"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-004",
      "EST-005",
      "EST-006"
    ],
    "status": 200
  }
}
```

---

### ESC-COO-04 · Candidatos a beca por rendimiento (ING-SIS)

- **Verifica:** R-BEC-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de mi programa cumplen los requisitos de la beca por rendimiento?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "Laura Gómez",
      "4.39"
    ],
    "noContiene": [
      "Camilo Rojas",
      "Valentina Castro",
      "Mateo Vargas"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ING-SIS · periodo 2026-2"
      },
      {
        "documento": "bienestar_y_becas.md",
        "seccion": "Sección 3.1"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-001",
      "EST-002",
      "EST-003"
    ],
    "status": 200
  }
}
```

---

### ESC-COO-05 · Candidatos a beca por rendimiento (ADM): ninguno

- **Verifica:** R-BEC-01, R-BEC-02
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de mi programa cumplen los requisitos de la beca por rendimiento?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-02"
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
      "Mateo Vargas",
      "determin"
    ],
    "noContiene": [
      "Laura Gómez",
      "Andrés Martínez",
      "Sofía Herrera"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ADM · periodo 2026-2"
      },
      {
        "documento": "bienestar_y_becas.md",
        "seccion": "Sección 3.1"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-004",
      "EST-005",
      "EST-006"
    ],
    "status": 200
  }
}
```

**Notas:** Nadie cumple: Camilo 3.88 y Valentina tiene perdidas. Mateo Vargas tiene Estadística sin definitiva: su elegibilidad es "no determinable todavía" (D-05), aunque ni con 5.0 en el corte 3 llegaría a 4.3 (decisión D-23: se mantiene "no determinable" en ambos casos). No debe listar a ningún estudiante como candidato.

---

### ESC-COO-06 · Pérdidas por inasistencia

- **Verifica:** R-ASI-01, R-ASI-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de mi programa perdieron alguna materia por inasistencia?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "Andrés Martínez",
      "Bases de Datos",
      "Sofía Herrera"
    ],
    "noContiene": [
      "Camilo Rojas",
      "Valentina Castro",
      "Mateo Vargas"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ING-SIS · periodo 2026-2"
      },
      {
        "documento": "reglamento_academico_2025.md",
        "seccion": "Artículo 11"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [
      "EST-001",
      "EST-002",
      "EST-003"
    ],
    "status": 200
  }
}
```

**Notas:** Andrés: 9/48 (18.75 %). Sofía: Inglés III sin dato, se menciona como no determinable, no como perdida.

---

### ESC-COO-07 · Definitiva de un estudiante de su programa

- **Verifica:** R-SEG-03, R-CAL-01
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es la definitiva de Valentina Castro en Microeconomía?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-02"
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
      "1.8"
    ],
    "noContiene": [
      "1.81"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ADM · periodo 2026-2"
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
      "EST-005"
    ],
    "status": 200
  }
}
```

---

### ESC-COO-08 · ¿Puede habilitar? (estudiante de su programa)

- **Verifica:** R-HAB-01, CL-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Puede Valentina Castro habilitar sus materias perdidas?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-02"
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
      "2.3",
      "Microeconomía",
      "1.8"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "reglamento_academico_2025.md",
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

**Notas:** Estadística (2.3) sí; Microeconomía (1.8 < 2.0) no.

---

### ESC-COO-09 · Asignatura sin definitiva de un estudiante

- **Verifica:** R-CAL-05, D-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cómo va Mateo Vargas en Estadística?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-02"
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
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ADM · periodo 2026-2"
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
      "EST-006"
    ],
    "status": 200
  }
}
```

---

### ESC-COO-10 · Promedio provisional de un estudiante

- **Verifica:** R-CAL-10, CL-26
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuál es el promedio de Sofía Herrera?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "3.34"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ING-SIS · periodo 2026-2"
      },
      {
        "documento": "evaluacion_y_calificaciones.md",
        "seccion": "Sección 4"
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

### ESC-COO-11 · Lista de estudiantes de su programa

- **Verifica:** R-SEG-03, §4
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes hay en mi programa?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "Laura Gómez",
      "Andrés Martínez",
      "Sofía Herrera"
    ],
    "noContiene": [
      "Camilo Rojas",
      "Valentina Castro",
      "Mateo Vargas"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ING-SIS · periodo 2026-2"
      }
    ]
  },
  "usoDatosEstudiante": true,
  "log": {
    "estudiantesConsultados": [],
    "status": 200
  }
}
```

**Notas:** `usoDatosEstudiante` es true porque se consulta `/programas/{id}/estudiantes` (R-RES-04). Listar un programa NO cuenta en `log.estudiantesConsultados` (R-LOG-06, D-24).

---

### ESC-COO-12 · Coordinador pregunta por "su" promedio

- **Verifica:** R-SEG-11, CL-30, D-16
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
      "rol": "coordinador",
      "id": "COORD-02"
    }
  }
}
```

**Output esperado**

```json
{
  "status": 200,
  "usoDatosEstudiante": false,
  "log": {
    "estudiantesConsultados": [],
    "status": 200
  }
}
```

**Notas:** 200, no 403: su rol no tiene datos académicos propios; puede consultar los de su programa.

---

### ESC-COO-13 · Nombre ambiguo: solo el nombre de pila

- **Verifica:** R-SEG-12, D-21
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cómo va Laura?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "4.39",
      "4.5"
    ]
  }
}
```

**Notas:** Se pide precisar con el ID; no se elige un estudiante ni se dan sus notas. No se exige el fragmento "ID": sin distinguir mayúsculas aparece dentro de muchas palabras y no comprueba nada.

## Casos 403

---

### ESC-COO-14 · Estudiante de otro programa por nombre

- **Verifica:** R-SEG-03, CL-10, AC-02, R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué notas tiene Valentina Castro?",
    "usuario": {
      "rol": "coordinador",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-COO-15 · Estudiante de otro programa por ID

- **Verifica:** R-SEG-03, CL-10, R-SEG-14
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
      "rol": "coordinador",
      "id": "COORD-02"
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

**Notas:** Se decide con las listas de ADM, sin consultar a EST-002 (R-SEG-03). Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-COO-16 · Listado de otro programa

- **Verifica:** R-SEG-03, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Qué estudiantes de Administración de Empresas tienen materias perdidas?",
    "usuario": {
      "rol": "coordinador",
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
  }
}
```

---

### ESC-COO-17 · Comparación con otro programa

- **Verifica:** R-SEG-07, D-13, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "Compara el promedio de mi programa con el de Ingeniería de Sistemas",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-02"
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

**Notas:** Todo o nada: no se responde la parte de su programa.

---

### ESC-COO-18 · Datos de otro coordinador

- **Verifica:** §4, R-ARQ-04
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
      "rol": "coordinador",
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
  }
}
```

---

### ESC-COO-19 · Estudiante inexistente

- **Verifica:** R-SEG-05, R-ARQ-04
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuáles son las notas de EST-099?",
    "usuario": {
      "rol": "coordinador",
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
  }
}
```

**Notas:** No revela si existe.

---

### ESC-COO-20 · Estudiante de otro programa sin nombrar el programa

- **Verifica:** R-SEG-03, R-SEG-14
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Mateo Vargas tiene faltas en Estadística?",
    "usuario": {
      "rol": "coordinador",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

## Barrera determinista de nombres (R-SEG-14)

---

### ESC-COO-21 · Solo el nombre de pila de un estudiante de otro programa

- **Verifica:** R-SEG-14, CL-10, D-28
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cómo va Valentina este periodo?",
    "usuario": {
      "rol": "coordinador",
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
    "status": 403,
    "motivo": "barrera determinista"
  },
  "tokens": 0,
  "costoUsd": 0
}
```

**Notas:** Lo resuelve la barrera determinista (R-SEG-14): el nombre o ID está en la lista de bloqueo, así que no se llama al LLM.

---

### ESC-COO-22 · Solo el apellido de un estudiante de otro programa

- **Verifica:** R-SEG-14, D-28
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Herrera tiene faltas?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-02"
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

### ESC-COO-23 · Nombre de un estudiante de su programa no se bloquea

- **Verifica:** R-SEG-14, R-SEG-03
- **Nivel:** api
- **Precondiciones:** api-simulada-levantada

**Input**

```json
{
  "metodo": "POST",
  "ruta": "/api/asistente/preguntar",
  "cuerpo": {
    "pregunta": "¿Cuántas faltas tiene Sofía Herrera en Bases de Datos?",
    "usuario": {
      "rol": "coordinador",
      "id": "COORD-01"
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
      "0"
    ]
  },
  "citas": {
    "incluye": [
      {
        "documento": "Datos del programa",
        "seccion": "ING-SIS · periodo 2026-2"
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

**Notas:** Los estudiantes de sus programas no están en la lista de bloqueo (R-SEG-14).
