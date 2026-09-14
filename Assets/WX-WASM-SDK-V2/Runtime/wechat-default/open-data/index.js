import { MessageType } from './common/messages';
import { createRankController } from './rank/controller';

const Layout = requirePlugin('Layout').default;
const sharedCanvas = wx.getSharedCanvas();
const sharedContext = sharedCanvas.getContext('2d');

const rankController = createRankController({
    Layout,
    canvas: sharedCanvas,
    context: sharedContext,
});

wx.onMessage((rawMessage) => {
    const message = parseMessage(rawMessage);
    if (!message || !message.type) {
        return;
    }

    switch (message.type) {
        case MessageType.WX_RENDER:
            rankController.resize(message);
            break;
        case MessageType.WX_SHOW:
            rankController.repaint();
            break;
        case MessageType.WX_DESTROY:
            rankController.destroy();
            break;
        case MessageType.SHOW_RANK:
        case MessageType.LEGACY_SHOW_FRIEND_RANK:
            rankController.show(message);
            break;
        case MessageType.REFRESH_RANK:
            rankController.refresh();
            break;
        default:
            console.warn(`[WX OpenData] unsupported message type: ${message.type}`);
            break;
    }
});

function parseMessage(message) {
    if (typeof message !== 'string') {
        return message;
    }

    try {
        return JSON.parse(message);
    }
    catch (error) {
        console.error('[WX OpenData] invalid message', error);
        return null;
    }
}
