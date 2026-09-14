import createRankTemplate, { createLoadingTemplate } from './template';
import { createLoadingStyle, createRankStyle } from './style';

export function createRankView({ Layout, canvas, context }) {
    function render(template, style) {
        Layout.clear();
        Layout.init(template, style);
        Layout.layout(context);
    }

    return {
        showRank(entries, options) {
            const hasSelf = entries.some(entry => entry.isSelf);
            render(
                createRankTemplate({ entries, scoreLabel: options.scoreLabel }),
                createRankStyle(getCanvasSize(canvas), hasSelf)
            );
        },

        showLoading() {
            render(
                createLoadingTemplate(),
                createLoadingStyle(getCanvasSize(canvas))
            );
        },

        updateViewport(viewport) {
            Layout.updateViewPort(viewport);
        },

        repaint() {
            Layout.repaint();
        },

        destroy() {
            Layout.clearAll();
        },
    };
}

function getCanvasSize(canvas) {
    return {
        width: canvas.width,
        height: canvas.height,
    };
}
