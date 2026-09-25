import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { createApi, mockMode } from './api/create';
import { createPendingStore } from './state/persistence';
import './styles.css';
async function bootstrap() {
  const api = await createApi();
  const store = createPendingStore(localStorage, mockMode ? 'mock' : 'real');
  createRoot(document.getElementById('root')!).render(<StrictMode><App api={api} store={store} simulated={mockMode} /></StrictMode>);
}
void bootstrap().catch(() => {
  const root = document.getElementById('root')!;
  root.textContent = 'Não foi possível iniciar o OrderFlow. Verifique se o armazenamento local está disponível e atualize a página. Preserve pendências existentes antes de limpar dados.';
  root.setAttribute('role', 'alert');
});
