# Road Gritting Predictor - Web UI

Minimal web interface for the Road Gritting ML Predictor API.

## Setup

```bash
npm install
cp .env.example .env
```

## Development

Ensure the Flask API is running on port 5000, then:

```bash
npm run dev
```

The Vite dev server will proxy `/api` requests to `http://localhost:5000`.

## Build

```bash
npm run build
```

Output in `dist/` directory.

## Environment Variables

- `VITE_API_URL` - API base URL (default: `/api`)
