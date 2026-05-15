# FAQ — Soporte interno de TI

**¿Cómo reseteo la VPN si no me conecta?** Abre el cliente Tailscale, pulsa "Logout" desde el menú del icono de la barra de tareas y vuelve a hacer login con tu correo corporativo de Acme Iberia. Si tras 2 intentos sigue sin funcionar, lanza el comando `tailscale netcheck` desde una terminal y pega el output en el canal `#it-support` de Slack mencionando a `@oncall-it`.

**¿Cómo pido un permiso de red para un nuevo servicio?** Rellena el formulario "Solicitud de regla de firewall" en el portal interno `it.acme-iberia.local`. Necesitas: IP o hostname origen, puerto destino, protocolo (TCP/UDP) y justificación de negocio en una frase. El SLA de aprobación es de 48 horas laborables; las urgencias se escalan por Slack a tu manager.

**¿Cuándo se rotan las API keys de producción?** Las keys con acceso a producción se rotan automáticamente cada 90 días desde Vault. Recibirás un correo 7 días antes del vencimiento con instrucciones para regenerar tu sesión local. No hace falta abrir ticket salvo que la rotación falle, en cuyo caso el equipo de plataforma te asistirá.

**¿A quién aviso si pierdo el portátil corporativo?** Manda un mensaje en `#seguridad` de Slack indicando "portátil perdido" y avisa a tu manager por teléfono. El equipo de seguridad bloquea remotamente el dispositivo en menos de 15 minutos y abre un ticket de incidente automático. No esperes: cuanto antes se reporte, antes se mitiga el riesgo.

**¿Cómo cambio mi nombre o pronombre en BambooHR?** Edita tu perfil en BambooHR yendo a "Mis datos" > "Personal". Los cambios se sincronizan con Slack y Google Workspace en aproximadamente 1 hora. Para cambiar el correo corporativo hace falta abrir un ticket en `#it-support` porque implica migración de buzón.
