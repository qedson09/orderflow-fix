import { useEffect, useRef, useState } from 'react';
import { ApiError, messageOf } from '../api/types';
// Sem sobreposição de requisições; aborta fetch e timer ao desmontar/trocar o ID.
export function usePolling<T>(load: (signal: AbortSignal) => Promise<T>, key: string, interval = 5000) {
    const loader = useRef(load);
    loader.current = load;
    const [state, setState] = useState<{ key: string; data?: T; error?: string; loading: boolean }>({ key, loading: true });
    useEffect(() => {
        const controller = new AbortController();
        let timer: ReturnType<typeof setTimeout>;
        let failures = 0;
        setState({ key, loading: true });
        async function tick() {
            let delay = interval;
            try {
                const data = await loader.current(controller.signal);
                if (!controller.signal.aborted) { failures = 0; setState({ key, data, loading: false }); }
            } catch (error) {
                failures++;
                delay = Math.max(Math.min(interval * 2 ** failures, 60000), error instanceof ApiError ? error.retryAfterMs : 0);
                if (!controller.signal.aborted) setState(previous => ({ ...previous, key, error: messageOf(error), loading: false }));
            } finally { if (!controller.signal.aborted) timer = setTimeout(tick, delay); }
        }
        void tick();
        return () => { controller.abort(); clearTimeout(timer); };
    }, [key, interval]);
    return state.key === key ? state : { key, loading: true };
}
