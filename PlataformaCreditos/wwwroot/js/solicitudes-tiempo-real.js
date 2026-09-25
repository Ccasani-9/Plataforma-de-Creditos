// Notificaciones en tiempo real de "Mis solicitudes" y "Detalle" vía WebSocket (ASP.NET Core SignalR).
// - Transporte forzado a WebSocket (visible en DevTools → Network → WS).
// - La identidad la determina el servidor a partir de la cookie de Identity: no se envía ningún UsuarioId.
// - Muestra el estado de la conexión, reconecta automáticamente y, al reconectar, consulta el estado
//   vigente en el servidor para recuperar cambios ocurridos durante la desconexión.
(function () {
    "use strict";

    const indicador = document.getElementById("estado-conexion");
    if (!indicador || typeof signalR === "undefined") {
        return;
    }

    const CLASES_BADGE = {
        Pendiente: "text-bg-warning",
        Aprobado: "text-bg-success",
        Rechazado: "text-bg-danger"
    };

    const ESTADOS_CONEXION = {
        conectando: { texto: "Conectando…", clase: "text-bg-secondary" },
        conectado: { texto: "Tiempo real: conectado", clase: "text-bg-success" },
        reconectando: { texto: "Reconectando…", clase: "text-bg-warning" },
        desconectado: { texto: "Desconectado", clase: "text-bg-danger" }
    };

    function mostrarConexion(estado) {
        const info = ESTADOS_CONEXION[estado];
        indicador.className = "badge " + info.clase;
        indicador.textContent = info.texto;
        indicador.dataset.estado = estado;
    }

    function mostrarAviso(titulo, mensaje, tipo) {
        const contenedor = document.getElementById("avisos-tiempo-real");
        if (!contenedor) {
            return;
        }

        const toast = document.createElement("div");
        toast.className = "toast align-items-center border-0 text-bg-" + (tipo || "primary");
        toast.setAttribute("role", "status");
        toast.setAttribute("aria-live", "polite");

        const fila = document.createElement("div");
        fila.className = "d-flex";
        const cuerpo = document.createElement("div");
        cuerpo.className = "toast-body";
        const fuerte = document.createElement("strong");
        fuerte.textContent = titulo;
        cuerpo.appendChild(fuerte);
        cuerpo.appendChild(document.createElement("br"));
        cuerpo.appendChild(document.createTextNode(mensaje));
        const cerrar = document.createElement("button");
        cerrar.type = "button";
        cerrar.className = "btn-close btn-close-white me-2 m-auto";
        cerrar.setAttribute("data-bs-dismiss", "toast");
        cerrar.setAttribute("aria-label", "Cerrar");
        fila.appendChild(cuerpo);
        fila.appendChild(cerrar);
        toast.appendChild(fila);
        contenedor.appendChild(toast);

        const instancia = bootstrap.Toast.getOrCreateInstance(toast, { delay: 8000 });
        toast.addEventListener("hidden.bs.toast", () => toast.remove());
        instancia.show();
    }

    function crearBadge(estado) {
        const badge = document.createElement("span");
        badge.className = "badge " + (CLASES_BADGE[estado] || "text-bg-secondary");
        badge.dataset.estado = estado;
        badge.textContent = estado;
        return badge;
    }

    /** Actualiza el DOM de la solicitud; devuelve true si su estado cambió. */
    function aplicarEstado(evento) {
        const elementos = document.querySelectorAll('[data-solicitud-id="' + evento.solicitudId + '"]');
        let cambio = false;

        elementos.forEach((elemento) => {
            const celdaEstado = elemento.querySelector('[data-campo="estado"]');
            if (celdaEstado) {
                const actual = celdaEstado.querySelector("[data-estado]");
                if (!actual || actual.dataset.estado !== evento.estado) {
                    cambio = true;
                    celdaEstado.replaceChildren(crearBadge(evento.estado));
                    elemento.classList.add("fila-actualizada");
                    setTimeout(() => elemento.classList.remove("fila-actualizada"), 3000);
                }
            }

            const texto = elemento.querySelector('[data-campo="estado-texto"]');
            if (texto) {
                texto.textContent = evento.estado;
            }

            const motivo = elemento.querySelector('[data-campo="motivo"]');
            if (motivo) {
                motivo.textContent = evento.motivoRechazo || (motivo.dataset.vacio ?? "");
            }
        });

        return cambio;
    }

    function describir(evento) {
        return evento.estado === "Rechazado"
            ? "Tu solicitud #" + evento.solicitudId + " fue rechazada. Motivo: " + (evento.motivoRechazo || "—")
            : "Tu solicitud #" + evento.solicitudId + " fue " + evento.estado.toLowerCase() + ".";
    }

    const conexion = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/solicitudes", {
            transport: signalR.HttpTransportType.WebSockets,
            skipNegotiation: true
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    conexion.on("SolicitudEstadoActualizado", (evento) => {
        aplicarEstado(evento);
        mostrarAviso("Solicitud actualizada", describir(evento), evento.estado === "Aprobado" ? "success" : "danger");
    });

    /** Consulta el estado vigente en el servidor y aplica los cambios perdidos durante la desconexión. */
    async function sincronizar() {
        const estados = await conexion.invoke("ObtenerEstadoActual");
        const cambiadas = estados.filter(aplicarEstado);
        if (cambiadas.length > 0) {
            mostrarAviso("Cambios recuperados", cambiadas.map(describir).join(" "), "info");
        }
    }

    conexion.onreconnecting(() => mostrarConexion("reconectando"));
    conexion.onreconnected(async () => {
        mostrarConexion("conectado");
        await sincronizar();
    });
    // Si se agotan los reintentos automáticos se sigue intentando cada 10 s.
    conexion.onclose(() => {
        mostrarConexion("desconectado");
        setTimeout(iniciar, 10000);
    });

    async function iniciar() {
        if (conexion.state !== signalR.HubConnectionState.Disconnected) {
            return;
        }
        mostrarConexion("conectando");
        try {
            await conexion.start();
            mostrarConexion("conectado");
            await sincronizar();
        } catch (error) {
            console.warn("No se pudo conectar al hub de solicitudes", error);
            mostrarConexion("desconectado");
            setTimeout(iniciar, 5000);
        }
    }

    iniciar();
})();
