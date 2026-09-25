# OrderFlow — frontend

Interface React para enviar ordens e acompanhar a decisão FIX persistida pelo OrderGenerator, com auditoria eventual independente.

Para executar a solução integrada, consulte [o README principal](../README.md). Os comandos de mock abaixo continuam disponíveis para desenvolvimento isolado.

## Tecnologias

- React 19, TypeScript 5.9, Vite 7 e CSS responsivo.
- Zod para validar contratos HTTP e registros locais.
- Vitest, Testing Library e jsdom para testes unitários e de integração do frontend.
- Docker multi-stage e Nginx sem privilégios para servir o build e encaminhar `/api`.
- Node.js 22.12+ ou 24; npm; versões resolvidas preservadas no package-lock.json.
