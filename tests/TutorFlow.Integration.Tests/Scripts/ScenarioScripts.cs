namespace TutorFlow.Integration.Tests.Scripts;

/// <summary>
/// One plausible script of the fake LLM for every <c>api</c> scenario of <c>escenarios/</c>, by scenario id: which tools a
/// real model would call and what it would answer. The scripts never contain the expected output of the scenario and
/// nothing that depends on the text is verified from them (escenarios/README section 4.4): they only make the real
/// backend (identity, defenses, use case, tools, citations, audit trail) run end to end without the network.
/// </summary>
/// <remarks>
/// A scenario without an entry here fails visibly (see <c>ApiScenarioTests</c>); there is no silent default. An entry
/// <see cref="ModelScript.NoModel"/> declares that the deterministic layers resolve the request before the model, and
/// the test fails if the backend calls the LLM anyway.
/// </remarks>
public static class ScenarioScripts
{
    private const string NoInformation = "No encuentro esa información en los documentos disponibles.";

    private static readonly Dictionary<string, ModelScript> Table = Build();

    /// <summary>Ids that have a script.</summary>
    public static IReadOnlyCollection<string> Ids => Table.Keys;

    /// <summary>The script of a scenario.</summary>
    /// <param name="scenarioId">Scenario id.</param>
    /// <returns>The script, or <see langword="null"/> if the scenario has none.</returns>
    public static ModelScript? For(string scenarioId) => Table.GetValueOrDefault(scenarioId);

    private static (string, string) Reg(int article) => ("reglamento_academico_2025.md", $"Artículo {article}");

    private static (string, string) Eval(int section) => ("evaluacion_y_calificaciones.md", $"Sección {section}");

    private static (string, string) Welfare(string section) => ("bienestar_y_becas.md", $"Sección {section}");

    // The model looks the topic up in the regulations and answers from the fragments it gets back.
    private static ModelScript Docs(string query, string answer, params (string, string)[] citations) =>
        ModelScript.Calls(Tool.Search(query)).Answer(answer, citations);

    // The model reads the data of the student (own data for a student) and answers from it.
    private static ModelScript Own(string answer, params (string, string)[] citations) =>
        ModelScript.Calls(Tool.Status()).Answer(answer, citations);

    // The model looks the data up and the topic in the regulations (two rounds of tools).
    private static ModelScript OwnAndDocs(string query, string answer, params (string, string)[] citations) =>
        ModelScript.Calls(Tool.Status()).Then(Tool.Search(query)).Answer(answer, citations);

    // A coordinator asks about a student by name: the model finds the id and then reads the data.
    private static ModelScript Student(string name, string id, string answer, params (string, string)[] citations) =>
        ModelScript.Calls(Tool.Find(name)).Then(Tool.Status(id)).Answer(answer, citations);

    private static ModelScript Summary(string answer, params (string, string)[] citations) =>
        ModelScript.Calls(Tool.ProgramSummary()).Answer(answer, citations);

    // D-32: the model refuses before calling any data tool.
    private static ModelScript Refuse(string reason = Tool.ThirdParty) => ModelScript.Reject(reason);

    private static Dictionary<string, ModelScript> Build()
    {
        var table = new Dictionary<string, ModelScript>(StringComparer.Ordinal);

        // Resolved by the deterministic layers, before the model: validation (400), identity (403), barrier of names and
        // ids (403), injection and extraction defenses (403) and the simulated API being down (503).
        foreach (var id in (string[])
        [
            "ESC-BAN-10",
            "ESC-EST-19", "ESC-EST-20", "ESC-EST-26", "ESC-EST-30", "ESC-EST-31", "ESC-EST-32",
            "ESC-COO-14", "ESC-COO-15", "ESC-COO-20", "ESC-COO-21", "ESC-COO-22",
            "ESC-SIM-12", "ESC-SIM-14",
            "ESC-INY-01", "ESC-INY-02", "ESC-INY-03", "ESC-INY-05", "ESC-INY-06", "ESC-INY-08", "ESC-INY-09",
            "ESC-INY-10", "ESC-INY-12", "ESC-INY-17", "ESC-INY-18",
            "ESC-ABU-10",
            "ESC-ENT-01", "ESC-ENT-02", "ESC-ENT-04", "ESC-ENT-07", "ESC-ENT-08", "ESC-ENT-09", "ESC-ENT-10",
            "ESC-ENT-11", "ESC-ENT-12", "ESC-ENT-13", "ESC-ENT-14", "ESC-ENT-15", "ESC-ENT-16", "ESC-ENT-17",
        ])
        {
            table[id] = ModelScript.NoModel;
        }

        // llm-cuota-agotada: the model is reached and answers 429 (CL-23).
        table["ESC-ABU-11"] = ModelScript.ModelFails;

        // ESC-BAN · banco de preguntas de ejemplo.
        table["ESC-BAN-01"] = Docs("nota mínima para aprobar", "Una asignatura se aprueba con una definitiva igual o superior a 3.0, en escala de 0.0 a 5.0.", Reg(5));
        table["ESC-BAN-02"] = Own("Tu promedio ponderado del periodo es 4.39.", Eval(4));
        table["ESC-BAN-03"] = Own("Tienes 2 faltas de 48 clases programadas en Bases de Datos.");
        table["ESC-BAN-04"] = Docs("nota definitiva", "La definitiva es 30% del corte 1, 30% del corte 2 y 40% del corte 3, redondeada a una cifra decimal.", Eval(2), Eval(3));
        table["ESC-BAN-05"] = OwnAndDocs("nota definitiva", "Tu nota definitiva en Contabilidad es 3.9.", Eval(3));
        table["ESC-BAN-06"] = Summary("Valentina Castro perdió Microeconomía (1.8) y Mateo Vargas perdió Estadística (2.3).");
        table["ESC-BAN-07"] = Docs("horario de atención de Bienestar", "Bienestar atiende en el horario indicado en el documento.", Welfare("1"));
        table["ESC-BAN-08"] = Docs("beca por rendimiento académico", "La beca por rendimiento exige los requisitos de la sección 3.1.", Welfare("3.1"));
        table["ESC-BAN-09"] = Docs("parqueaderos", NoInformation);

        // ESC-EST · preguntas de un estudiante.
        table["ESC-EST-01"] = Own("Tu nota definitiva en Bases de Datos es la que figura en tus cortes.", Eval(3));
        table["ESC-EST-02"] = OwnAndDocs("prueba académica", "Según tu promedio ponderado, estás en prueba académica.", Reg(20));
        table["ESC-EST-03"] = OwnAndDocs("habilitación", "Puedes habilitar las asignaturas con definitiva entre 2.0 y 2.9 que sean habilitables.", Reg(6), Reg(7));
        table["ESC-EST-04"] = OwnAndDocs("habilitación", "Microeconomía se puede habilitar si tu definitiva está en el rango del reglamento.", Reg(6));
        table["ESC-EST-05"] = Own("Este es tu promedio ponderado del periodo.", Eval(4));
        table["ESC-EST-06"] = Own("Esta es tu nota en Estadística con los cortes registrados.");
        table["ESC-EST-07"] = Own("Este es tu promedio ponderado, provisional por asignaturas sin definitiva.", Eval(4));
        table["ESC-EST-08"] = Own("Este es tu promedio del periodo.", Eval(4));
        table["ESC-EST-09"] = Own("Estas son tus faltas en Inglés III.");
        table["ESC-EST-10"] = OwnAndDocs("beca por rendimiento académico", "Con tus datos cumples los requisitos de la beca por rendimiento.", Welfare("3.1"));
        table["ESC-EST-11"] = OwnAndDocs("beca por rendimiento académico", "Con tus datos no cumples los requisitos de la beca por rendimiento.", Welfare("3.1"));
        table["ESC-EST-12"] = OwnAndDocs("pérdida de cupo", "Pierde el cupo quien queda en prueba académica dos periodos consecutivos.", Reg(21));
        table["ESC-EST-13"] = Docs("cancelación de asignaturas", "Puedes cancelar una asignatura hasta la semana 8 del periodo.", Reg(15));
        table["ESC-EST-14"] = ModelScript.Calls(Tool.Cancel("Programación Orientada a Objetos", "Bases de Datos"))
            .Then(Tool.Search("mínimo de créditos")).Answer("Si cancelas ambas, conservas menos del mínimo de 8 créditos.", Reg(16));
        table["ESC-EST-15"] = Docs("reclamo de notas", "Puedes reclamar dentro del plazo de la sección de reclamos, ante el docente.", Eval(5));
        table["ESC-EST-16"] = Docs("excusa médica", "Presenta la excusa médica ante tu coordinador para que no cuente como inasistencia.", Reg(12));
        table["ESC-EST-17"] = ModelScript.Calls(Tool.Habilitation(4.5)).Then(Tool.Search("habilitación")).Answer("La habilitación reemplaza la definitiva con un máximo de 3.0.", Reg(6));
        table["ESC-EST-18"] = Docs("beca de apoyo socioeconómico", "La beca de apoyo socioeconómico se solicita en Bienestar según los estratos indicados.", Welfare("3.2"));
        foreach (var id in new[] { "ESC-EST-21", "ESC-EST-22", "ESC-EST-23", "ESC-EST-24", "ESC-EST-25", "ESC-EST-27", "ESC-EST-28", "ESC-EST-29" })
        {
            table[id] = Refuse();
        }

        table["ESC-EST-33"] = Own("Tu promedio ponderado del periodo es 4.39.", Eval(4));

        // ESC-COO · preguntas de un coordinador.
        table["ESC-COO-01"] = Summary("Estos son los estudiantes de tu programa con alguna asignatura perdida.");
        table["ESC-COO-02"] = Summary("Estos son los estudiantes de tu programa en prueba académica.", Reg(20));
        table["ESC-COO-03"] = Summary("Estos son los estudiantes de tu programa en prueba académica.", Reg(20));
        table["ESC-COO-04"] = Summary("Estos estudiantes cumplen los requisitos de la beca por rendimiento.", Welfare("3.1"));
        table["ESC-COO-05"] = Summary("Para este estudiante no es determinable si cumple los requisitos de la beca.", Welfare("3.1"));
        table["ESC-COO-06"] = Summary("Estos estudiantes perdieron una asignatura por inasistencia.", Reg(11));
        table["ESC-COO-07"] = Student("Valentina Castro", "EST-005", "Esta es la definitiva de Valentina Castro en Microeconomía.");
        table["ESC-COO-08"] = Student("Valentina Castro", "EST-005", "Valentina Castro puede habilitar las asignaturas que cumplan el rango del reglamento.", Reg(6));
        table["ESC-COO-09"] = Student("Mateo Vargas", "EST-006", "Así va Mateo Vargas en Estadística.");
        table["ESC-COO-10"] = Student("Sofía Herrera", "EST-003", "Este es el promedio ponderado de Sofía Herrera.", Eval(4));
        table["ESC-COO-11"] = ModelScript.Calls(Tool.ListStudents()).Answer("Estos son los estudiantes de tu programa.");
        table["ESC-COO-12"] = ModelScript.Direct("Como coordinador no tienes notas propias; puedo consultar los estudiantes de tu programa.");
        table["ESC-COO-13"] = ModelScript.Calls(Tool.Find("Laura")).Answer("El nombre es incompleto o ambiguo: indícame el ID del estudiante.");
        // COO-16 and COO-19 do not use `rechazar` on purpose: the model asks for data outside the programs in charge and the
        // tool authorization (R-SEG-03, R-SEG-06) answers the 403, with nothing queried.
        table["ESC-COO-16"] = ModelScript.Calls(Tool.ProgramSummary("ADM")).Answer("No puedo consultar ese programa.");
        table["ESC-COO-17"] = Refuse();
        table["ESC-COO-18"] = Refuse();
        table["ESC-COO-19"] = ModelScript.Calls(Tool.Status("EST-099")).Answer("No puedo consultar a ese estudiante.");
        table["ESC-COO-23"] = Student("Sofía Herrera", "EST-003", "Estas son las faltas de Sofía Herrera en Bases de Datos.");

        // ESC-SIM · simulación de notas (los unitarios se ejecutan en Domain.Tests).
        table["ESC-SIM-08"] = ModelScript.Calls(Tool.Simulate("Estadística")).Answer("Esta es la nota mínima que necesitas en el corte 3 de Estadística.", Eval(3));
        table["ESC-SIM-09"] = ModelScript.Calls(Tool.Simulate("Estadística", 5.0)).Answer("Esa meta no es alcanzable con el corte 3.", Eval(3));
        table["ESC-SIM-10"] = ModelScript.Calls(Tool.Simulate("Bases de Datos")).Answer("Esta es la nota mínima que necesitas en el corte pendiente.", Eval(3));
        table["ESC-SIM-11"] = ModelScript.Calls(Tool.Simulate("Cálculo Integral")).Answer("Esta es la nota mínima que necesitas en el corte 3.", Eval(3));
        table["ESC-SIM-13"] = ModelScript.Calls(Tool.Simulate("Estadística", null, "EST-006")).Answer("Esta es la nota mínima que necesita en el corte 3.", Eval(3));

        // ESC-CIT · citas reales.
        table["ESC-CIT-01"] = Docs("habilitación", "Puedes habilitar desde una definitiva de 2.0.", Reg(6));
        table["ESC-CIT-02"] = Docs("pérdida por inasistencia", "Pierdes la asignatura si faltas a más del 15% de las clases programadas.", Reg(11));
        table["ESC-CIT-03"] = Docs("excusa médica", "Las faltas con excusa aprobada no se cuentan.", Reg(12));
        table["ESC-CIT-04"] = Docs("prueba académica", "Queda en prueba académica quien tiene un promedio ponderado bajo.", Reg(20));
        table["ESC-CIT-05"] = Docs("pérdida de cupo", "Se pierde el cupo tras dos periodos consecutivos en prueba académica.", Reg(21));
        table["ESC-CIT-06"] = Docs("protección de datos", "Solo tú y los coordinadores autorizados pueden consultar tus notas.", Reg(22));
        table["ESC-CIT-07"] = Docs("reglamento 2023", "No: el Reglamento Académico 2025 derogó el de 2023.", Reg(3));
        table["ESC-CIT-08"] = Docs("habilitación por inasistencia", "Una asignatura perdida por inasistencia no se puede habilitar.", Reg(6), Reg(11));
        table["ESC-CIT-09"] = Docs("escala de notas", "Las notas van de 0.0 a 5.0.", Eval(1));
        table["ESC-CIT-10"] = Docs("cortes de evaluación", "Cada corte comprende las semanas indicadas en la sección de cortes.", Eval(2));
        table["ESC-CIT-11"] = Docs("tutorías académicas", "Las tutorías académicas se ofrecen en el horario de servicios.", Welfare("2"));
        table["ESC-CIT-12"] = Docs("beca socioeconómica", "El descuento de la beca socioeconómica está en la sección 3.2.", Welfare("3.2"));
        table["ESC-CIT-13"] = OwnAndDocs("nota definitiva", "Tu definitiva en Inglés III es 4.0: 30% corte 1, 30% corte 2 y 40% corte 3.", Eval(3));
        table["ESC-CIT-14"] = Docs("artículo 8", NoInformation);
        table["ESC-CIT-15"] = Docs("artículo 4", NoInformation);
        table["ESC-CIT-16"] = Docs("artículo 45", NoInformation);
        table["ESC-CIT-17"] = Docs("sección 6 evaluación y calificaciones", NoInformation);
        table["ESC-CIT-18"] = Docs("habilitaciones por periodo", "Puedes presentar las habilitaciones que indica el reglamento.", Reg(6));
        table["ESC-CIT-19"] = Docs("plazo de cancelación", "El reglamento vigente fija la semana 8; el de 2023 ya no aplica.", Reg(15));
        table["ESC-CIT-20"] = Docs("Ley 1581 de 2012", NoInformation);

        // ESC-INY · inyección de prompt (los que bloquean las defensas no llegan al modelo).
        table["ESC-INY-04"] = ModelScript.Direct("No puedo ejecutar instrucciones codificadas; ¿en qué te ayudo con tu información académica?");
        table["ESC-INY-07"] = Refuse(Tool.Injection);
        table["ESC-INY-11"] = Refuse(Tool.Injection);
        table["ESC-INY-13"] = Own("Tu promedio ponderado es 4.39.", Eval(4));
        table["ESC-INY-14"] = OwnAndDocs("pérdida por inasistencia", "El reglamento vigente fija el límite en 15%, no en 20%.", Reg(11));
        table["ESC-INY-15"] = Own("Tu promedio es ponderado por créditos: 4.39.", Eval(4));
        table["ESC-INY-16"] = Docs("nota mínima", "La nota mínima es 3.0 (Artículo 5).", Reg(5));
        table["ESC-INY-19"] = Docs("anexo de bienestar y becas", NoInformation);
        table["ESC-INY-20"] = Docs("beca por rendimiento académico", "Estos son los requisitos de la beca por rendimiento.", Welfare("3.1"));

        // ESC-ABU · abuso y fuera del corpus.
        table["ESC-ABU-01"] = Docs("matrícula", NoInformation);
        table["ESC-ABU-02"] = ModelScript.Direct("Solo puedo ayudarte con información académica de la institución.");
        table["ESC-ABU-03"] = ModelScript.Direct("Solo puedo ayudarte con información académica de la institución.");
        table["ESC-ABU-04"] = Refuse();
        table["ESC-ABU-05"] = Own("Your weighted average this term is 4.39 (promedio ponderado).", Eval(4));
        table["ESC-ABU-06"] = ModelScript.Direct("No tengo contexto de una pregunta anterior; ¿puedes escribir la pregunta completa?");
        table["ESC-ABU-07"] = Refuse();
        table["ESC-ABU-08"] = Docs("créditos máximos de matrícula", NoInformation);
        table["ESC-ABU-09"] = Docs("nota mínima", "La nota mínima para aprobar es 3.0.", Reg(5));

        // ESC-ENT · entradas inválidas (las 200 llegan al modelo).
        table["ESC-ENT-03"] = Docs("nota mínima para aprobar", "La nota mínima para aprobar es 3.0.", Reg(5));
        table["ESC-ENT-05"] = Docs("nota mínima para aprobar", "La nota mínima para aprobar es 3.0.", Reg(5));
        table["ESC-ENT-06"] = ModelScript.Direct(NoInformation);
        table["ESC-ENT-18"] = Own("Tu promedio ponderado es 4.39.", Eval(4));

        // ESC-RAR · casos raros (los unitarios se ejecutan en Domain.Tests). Los códigos que no son del usuario no son su ID.
        table["ESC-RAR-18"] = Own("Esa referencia no corresponde a una asignatura tuya; esta es tu situación.");
        table["ESC-RAR-19"] = Own("Esa referencia no corresponde a una asignatura tuya; estas son tus notas.");
        table["ESC-RAR-20"] = Own("Esa asignatura no aparece entre las tuyas; estas son tus asignaturas.");

        return table;
    }
}
