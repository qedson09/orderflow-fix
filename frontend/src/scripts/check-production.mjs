import { readdir, readFile } from 'node:fs/promises';
for (const name of await readdir('dist/assets')) {
  if (name.endsWith('.js') && /ORDERFLOW_DEVELOPMENT_ADAPTER_ONLY|orderflow:mock:orders:v1/.test(await readFile(`dist/assets/${name}`, 'utf8'))) {
    throw new Error('Adaptador simulado incluído em produção');
  }
}
console.log('Produção verificada: adaptador simulado ausente.');
