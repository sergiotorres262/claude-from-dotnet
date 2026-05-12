# 00 — Setup

Esto es lo que necesitas para clonar el repo, configurar las credenciales y correr el primer ejemplo. Si ya tienes .NET 8 y una API key de Anthropic, salta al final.

---

## 1. Conseguir una API key de Anthropic

1. Crea cuenta o entra en [console.anthropic.com](https://console.anthropic.com).
2. Ve a **Settings → API Keys**.
3. Pulsa **Create Key**, dale un nombre (por ejemplo `claude-from-dotnet-local`).
4. Copia la key. Empieza por `sk-ant-api03-...`. **La verás una sola vez** — si la pierdes, hay que generar otra.

> **Importante.** La key da acceso de pago a tu cuenta. No la pegues en commits, capturas, issues, ni en `.env.example`. Si por error la expones (commit público, screenshot en LinkedIn, etc.), revócala inmediatamente en la misma pantalla y crea otra.

Anthropic da algo de saldo gratuito al crear la cuenta. Cada ejemplo de este repo consume del orden de céntimos por ejecución — para aprender llega de sobra.

---

## 2. Instalar .NET 8 SDK

El repo apunta a **.NET 8 (LTS)**. .NET 9 también funciona pero la versión LTS es la recomendada y la que usa el CI.

### Windows

Opción rápida con winget:

```powershell
winget install Microsoft.DotNet.SDK.8
```

Opción manual: descarga desde [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) → **.NET 8 SDK** → Windows x64.

### macOS

```bash
brew install --cask dotnet-sdk
```

O descarga manual desde el enlace de arriba (paquete `.pkg`).

### Linux (Ubuntu/Debian)

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

Para otras distros: [docs de Microsoft](https://learn.microsoft.com/en-us/dotnet/core/install/linux).

### Verificar instalación

```bash
dotnet --list-sdks
```

Debes ver al menos una línea `8.0.xxx`. Puedes tener varias versiones instaladas a la vez sin problema.

---

## 3. Clonar el repo

```bash
git clone https://github.com/sergiotorres262/claude-from-dotnet.git
cd claude-from-dotnet
```

---

## 4. Abrirlo en tu editor

Cualquiera de estos vale. El repo trae `.editorconfig` para que el estilo sea consistente en todos.

- **VS Code** — instala la extensión [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) de Microsoft. Abre la carpeta con `code .`.
- **Rider** — abre `ClaudeFromDotnet.sln`.
- **Visual Studio 2022** — abre `ClaudeFromDotnet.sln`.

---

## 5. Correr el primer ejemplo

```bash
cd examples/01-chat-basico
cp .env.example .env       # En Windows PowerShell: Copy-Item .env.example .env
# Edita .env y pega tu ANTHROPIC_API_KEY
dotnet run
```

Salida esperada (el texto exacto cambia):

```
--- Respuesta de claude-sonnet-4-6 (stop_reason: end_turn) ---
Soy Claude, un asistente de IA de Anthropic. ...
---
Tokens input: 40 / output: 83
```

Si ves esto, todo está listo. Pasa al README del ejemplo para entender cómo funciona, y de ahí al siguiente.

---

## 6. Problemas comunes

- **`'dotnet' no se reconoce como comando`** → reinicia la terminal después de instalar el SDK (el PATH se actualiza al abrir una sesión nueva).
- **`401 Unauthorized`** → la `ANTHROPIC_API_KEY` del `.env` está vacía o no es válida. Genera otra en la consola de Anthropic.
- **`Falta ANTHROPIC_API_KEY en .env`** → no copiaste `.env.example` a `.env`, o copiaste pero dejaste la línea sin valor.
- **El programa lanza algo pero no llega ninguna respuesta** → revisa que tu red no esté bloqueando `api.anthropic.com`. Algunas VPNs corporativas lo filtran.
- **`429 Too Many Requests`** → has gastado el rate limit. Espera 30-60 segundos. El ejemplo 02 introduce reintentos.

---

## 7. Siguiente paso

- [Glosario](glosario.md) — qué es un token, qué es streaming, qué es tool use, qué es MCP, qué es RAG.
- [Resumen de la API](api-reference-resumen.md) — los endpoints de Anthropic que se usan en el repo.
- [Ejemplo 01](../examples/01-chat-basico/README.md) — primera llamada explicada paso a paso.
