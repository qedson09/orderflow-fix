# OrderFlow — frontend

Interface React para enviar ordens e acompanhar a decisão FIX persistida pelo OrderGenerator, com auditoria eventual independente.

Para executar a solução integrada, consulte [o README principal](../README.md). Os comandos de mock abaixo continuam disponíveis para desenvolvimento isolado.

> This is a challenge by [Coodesh](https://coodesh.com/)

## Tecnologias

- React 19, TypeScript 5.9, Vite 7 e CSS responsivo.
- Zod para validar contratos HTTP e registros locais.
- Vitest, Testing Library e jsdom para testes unitários e de integração do frontend.
- Docker multi-stage e Nginx sem privilégios para servir o build e encaminhar `/api`.
- Node.js 22.12+ ou 24; npm; versões resolvidas preservadas no package-lock.json.

## Instalar e executar

Para a API de demonstração:

Na pasta `frontend`:

```bash
npm ci
npm run dev:mock
```

Abra http://localhost:5173. A faixa de demonstração identifica que todas as respostas são simuladas. PETR4/VALE3 recebem aceite após aproximadamente 2,5 segundos; VIIA4 recebe rejeição simulada. A auditoria aparece a partir de 9 segundos da criação, na próxima consulta. Não há simulação de exposição acumulada ou motor financeiro. Dados de demonstração sobrevivem ao reload em localStorage.

Para a API real:

```bash
cp .env.example .env.local
npm run dev
```

Em PowerShell, use `Copy-Item .env.example .env.local`. Ajuste API_PROXY_TARGET para a URL do Generator. O proxy do Vite encaminha `/api` sem CORS no navegador. Reinicie o Vite após alterar `.env.local`.

```bash
npm test
npm run typecheck
npm run build
npm run preview
```

O build de produção **sempre usa API real**, mesmo se solicitado com `--mode mock`. O script de build procura marcadores do adaptador simulado nos bundles. `preview` serve o build em http://localhost:4173 para inspeção; para integração de produção use Nginx/Docker. O modo simulado existe apenas no servidor de desenvolvimento.

## Estrutura

```text
orderflow-fix/
├── compose.frontend.yaml
└── frontend/
    ├── package.json / package-lock.json
    ├── tsconfig.json / vite.config.ts / index.html
    ├── Dockerfile / nginx.conf / .dockerignore / .gitignore
    ├── .env.example
    ├── docs/HTTP-CONTRACT.md
    ├── scripts/check-production.mjs
    └── src/
        ├── main.tsx / App.tsx / App.test.tsx / styles.css
        ├── api/          # interface, HTTP real, fábrica e mock de desenvolvimento
        ├── domain/       # schemas, identidade, validação e cálculo em centavos
        ├── state/        # coordenador de submissão, persistência e polling
        ├── components/   # formulário, resultado, ordens e auditoria
        └── test/         # setup e fixtures
```

## Referências técnicas

- [Vite: documentação](https://vite.dev/guide/)
- [Vitest: documentação](https://vitest.dev/guide/)
- [React: documentação](https://react.dev/)