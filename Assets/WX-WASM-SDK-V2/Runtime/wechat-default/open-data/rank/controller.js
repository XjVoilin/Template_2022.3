import { loadFriendRank } from './data';
import { createRankView } from './view';

const DEFAULT_SCORE_LABEL = '只';

export function createRankController(dependencies) {
    const view = createRankView(dependencies);

    let visible = false;
    let requestVersion = 0;
    let currentOptions = null;
    let currentState = { type: 'idle' };

    async function show(message = {}) {
        const options = normalizeOptions(message);
        if (!options) {
            console.error('[WX Rank] showRank requires non-empty key and selfOpenId');
            return;
        }

        visible = true;
        currentOptions = options;
        await loadAndRender();
    }

    async function refresh() {
        if (!visible) {
            return;
        }

        await loadAndRender();
    }

    function resize(message) {
        const ratio = positiveNumber(message.devicePixelRatio, 1);

        view.updateViewport({
            x: numberOrDefault(message.x, 0) / ratio,
            y: numberOrDefault(message.y, 0) / ratio,
            width: numberOrDefault(message.width, 0) / ratio,
            height: numberOrDefault(message.height, 0) / ratio,
        });

        if (visible) {
            renderCurrentState();
        }
    }

    function repaint() {
        if (visible) {
            view.repaint();
        }
    }

    function destroy() {
        visible = false;
        requestVersion += 1;
        currentOptions = null;
        currentState = { type: 'idle' };
        view.destroy();
    }

    async function loadAndRender() {
        const currentVersion = ++requestVersion;
        setState({ type: 'loading' });

        try {
            const entries = await loadFriendRank(
                currentOptions.key,
                currentOptions.selfOpenId
            );
            if (!isCurrentRequest(currentVersion)) {
                return;
            }

            if (!entries.length) {
                console.error('[WX Rank] friend rank is empty; self entry was expected');
                return;
            }

            if (!entries.some(entry => entry.isSelf)) {
                console.error('[WX Rank] self entry is missing from friend rank');
            }

            setState({ type: 'rank', entries });
        }
        catch (error) {
            if (!isCurrentRequest(currentVersion)) {
                return;
            }

            console.error('[WX Rank] load failed', error);
        }
    }

    function isCurrentRequest(version) {
        return visible && version === requestVersion;
    }

    function setState(state) {
        currentState = state;
        renderCurrentState();
    }

    function renderCurrentState() {
        switch (currentState.type) {
            case 'loading':
                view.showLoading();
                break;
            case 'rank':
                view.showRank(currentState.entries, currentOptions);
                break;
            default:
                break;
        }
    }

    return {
        show,
        refresh,
        resize,
        repaint,
        destroy,
    };
}

function normalizeOptions(message) {
    const key = nonEmptyString(message.key, null);
    const selfOpenId = nonEmptyString(message.selfOpenId, null);
    if (!key || !selfOpenId) {
        return null;
    }

    return {
        key,
        selfOpenId,
        scoreLabel: nonEmptyString(message.scoreLabel, DEFAULT_SCORE_LABEL),
    };
}

function nonEmptyString(value, fallback) {
    return typeof value === 'string' && value.trim() ? value : fallback;
}

function numberOrDefault(value, fallback) {
    return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function positiveNumber(value, fallback) {
    const number = numberOrDefault(value, fallback);
    return number > 0 ? number : fallback;
}
