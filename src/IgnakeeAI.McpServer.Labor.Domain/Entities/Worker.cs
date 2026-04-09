using IgnakeeAI.McpServer.Labor.Domain.Enums;

namespace IgnakeeAI.McpServer.Labor.Domain.Entities
{
    /// <summary>
    /// Trabajador registrado en la plataforma de mano de obra.
    /// Cada trabajador expone su tarifa, especialidad, ubicación actual y perfil web,
    /// permitiendo al agente IA buscar y valorar recursos humanos para una partida.
    ///
    /// CAMPOS CLAVE PARA LA VALORACIÓN INTELIGENTE:
    ///   - ProfileUrl: enlace a la página web/perfil online del trabajador.
    ///   - Latitude/Longitude: posición GPS actual del trabajador para búsqueda por proximidad.
    ///   - HourlyRate/DailyRate: coste de contratación para presupuesto de mano de obra.
    ///   - QualityRating: permite al agente comparar calidades entre trabajadores.
    ///   - Status: disponibilidad en tiempo real para planificación de obra.
    /// </summary>
    public class Worker
    {
        public int Id { get; set; }

        /// <summary>Código único del trabajador en la plataforma.</summary>
        public string WorkerId { get; set; } = default!;

        /// <summary>Nombre completo del trabajador.</summary>
        public string FullName { get; set; } = default!;

        /// <summary>Especialidad principal (ej. "albañil", "electricista", "fontanero", "pintor", "carpintero").</summary>
        public string Specialty { get; set; } = default!;

        /// <summary>Palabras clave para búsqueda semántica (separadas por coma).</summary>
        public string Keywords { get; set; } = string.Empty;

        /// <summary>Tarifa por hora en la moneda indicada.</summary>
        public decimal HourlyRate { get; set; }

        /// <summary>Tarifa por jornada completa (opcional).</summary>
        public decimal? DailyRate { get; set; }

        /// <summary>Moneda ISO 4217.</summary>
        public string Currency { get; set; } = "EUR";

        /// <summary>Latitud GPS de la ubicación actual del trabajador.</summary>
        public double? Latitude { get; set; }

        /// <summary>Longitud GPS de la ubicación actual del trabajador.</summary>
        public double? Longitude { get; set; }

        /// <summary>Dirección legible de la ubicación actual.</summary>
        public string? LocationAddress { get; set; }

        /// <summary>Zona de trabajo habitual (ej. "Madrid Norte", "Barcelona Centro").</summary>
        public string? WorkZone { get; set; }

        /// <summary>URL del perfil o página web personal del trabajador.</summary>
        public string? ProfileUrl { get; set; }

        /// <summary>Teléfono de contacto directo.</summary>
        public string? ContactPhone { get; set; }

        /// <summary>Correo electrónico de contacto.</summary>
        public string? ContactEmail { get; set; }

        /// <summary>Estado de disponibilidad actual.</summary>
        public WorkerStatus Status { get; set; } = WorkerStatus.Available;

        /// <summary>Horario de disponibilidad (ej. "L-V 07:00-17:00").</summary>
        public string? AvailabilitySchedule { get; set; }

        /// <summary>Años de experiencia en la especialidad.</summary>
        public int? ExperienceYears { get; set; }

        /// <summary>Valoración de calidad (1–5). Permite comparar trabajadores.</summary>
        public int? QualityRating { get; set; }

        /// <summary>Fecha de la última actualización del registro.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Indica si el trabajador está activo en la plataforma.</summary>
        public bool IsActive { get; set; } = true;
    }
}
