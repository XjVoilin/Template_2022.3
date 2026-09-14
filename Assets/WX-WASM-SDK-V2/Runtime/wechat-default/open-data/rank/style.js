const DESIGN_WIDTH = 1080;

const COLORS = Object.freeze({
    firstBackground: '#FFF159',
    firstForeground: '#C35E00',
    secondBackground: '#CBE8FF',
    secondForeground: '#515963',
    thirdBackground: '#F7D9BD',
    thirdForeground: '#63401F',
    normalBackground: '#FFFEEE',
    normalForeground: '#515963',
    normalScore: '#E99031',
    selfBackground: '#D9E971',
    selfForeground: '#587A0C',
});

export function createRankStyle({ width, height }, hasSelf = false) {
    const px = createScaler(width);
    const rowWidth = Math.max(0, width - px(50));
    const selfHeight = hasSelf ? px(160) : 0;

    return {
        container: {
            width,
            height,
        },
        list: {
            width: rowWidth,
            height: Math.max(0, height - selfHeight),
            marginLeft: px(25),
        },

        firstRankItem: createListRowStyle(rowWidth, COLORS.firstBackground, px, px(18)),
        secondRankItem: createListRowStyle(rowWidth, COLORS.secondBackground, px),
        thirdRankItem: createListRowStyle(rowWidth, COLORS.thirdBackground, px),
        rankItem: createListRowStyle(rowWidth, COLORS.normalBackground, px),
        selfRankItem: createSelfRowStyle(width, COLORS.selfBackground, px),

        firstRankBadge: createTopRankBadgeStyle(86, 89, px),
        secondRankBadge: createTopRankBadgeStyle(76, 79, px),
        thirdRankBadge: createTopRankBadgeStyle(76, 79, px),
        rankBadge: createListRankBadgeStyle(px),
        selfRankBadge: createSelfRankBadgeStyle(px),

        firstRankNumber: createTopRankNumberStyle(COLORS.firstForeground, px),
        secondRankNumber: createTopRankNumberStyle(COLORS.secondForeground, px),
        thirdRankNumber: createTopRankNumberStyle(COLORS.thirdForeground, px),
        rankNumber: createListRankNumberStyle(COLORS.normalForeground, px),
        selfRankNumber: createSelfRankNumberStyle(COLORS.selfForeground, px),

        avatarContainer: {
            position: 'absolute',
            left: px(143),
            top: px(11),
            width: px(128),
            height: px(128),
        },
        selfAvatarContainer: {
            position: 'absolute',
            left: px(162),
            top: px(16),
            width: px(128),
            height: px(128),
        },
        avatar: {
            position: 'absolute',
            left: px(9),
            top: px(9),
            width: px(110),
            height: px(110),
            borderRadius: px(55),
        },
        avatarOuterBackground: {
            position: 'absolute',
            left: 0,
            top: 0,
            width: px(128),
            height: px(128),
            borderRadius: px(64),
            backgroundColor: '#CCCCCC',
        },
        avatarInnerBackground: {
            position: 'absolute',
            left: px(3),
            top: px(3),
            width: px(122),
            height: px(122),
            borderRadius: px(61),
        },

        firstNickname: createListNicknameStyle(COLORS.firstForeground, px),
        secondNickname: createListNicknameStyle(COLORS.secondForeground, px),
        thirdNickname: createListNicknameStyle(COLORS.thirdForeground, px),
        nickname: createListNicknameStyle(COLORS.normalForeground, px),
        selfNickname: createSelfNicknameStyle(COLORS.selfForeground, px),

        firstScore: createListScoreStyle(COLORS.firstForeground, px),
        secondScore: createListScoreStyle(COLORS.secondForeground, px),
        thirdScore: createListScoreStyle(COLORS.thirdForeground, px),
        score: createListScoreStyle(COLORS.normalScore, px),
        selfScore: createSelfScoreStyle(COLORS.selfForeground, px),
    };
}

export function createLoadingStyle({ width, height }) {
    const px = createScaler(width);

    return {
        loadingRoot: {
            width,
            height,
            flexDirection: 'row',
            justifyContent: 'center',
            alignItems: 'center',
        },
        loadingGoose: {
            width: px(230),
            height: px(198),
        },
        loadingText: {
            marginLeft: px(24),
            width: px(260),
            height: px(60),
            lineHeight: px(60),
            fontSize: px(36),
            color: COLORS.normalForeground,
            textAlign: 'left',
        },
    };
}

function createListRowStyle(width, backgroundColor, px, marginTop = 0) {
    return {
        position: 'relative',
        width,
        height: px(150),
        marginTop,
        marginBottom: px(30),
        borderRadius: px(20),
        backgroundColor,
    };
}

function createSelfRowStyle(width, backgroundColor, px) {
    return {
        position: 'relative',
        width,
        height: px(160),
        borderRadius: px(20),
        backgroundColor,
    };
}

function createTopRankBadgeStyle(width, height, px) {
    return {
        position: 'absolute',
        left: px((147 - width) / 2),
        top: px((150 - height) / 2),
        width: px(width),
        height: px(height),
    };
}

function createListRankBadgeStyle(px) {
    return {
        position: 'absolute',
        left: 0,
        top: 0,
        width: px(147),
        height: px(150),
    };
}

function createSelfRankBadgeStyle(px) {
    return {
        position: 'absolute',
        left: 0,
        top: 0,
        width: px(164),
        height: px(160),
    };
}

function createTopRankNumberStyle(color, px) {
    return createTextStyle(px(72), px(72), px(48), color, 'center');
}

function createListRankNumberStyle(color, px) {
    return createTextStyle(px(147), px(150), px(62), color, 'center');
}

function createSelfRankNumberStyle(color, px) {
    return createTextStyle(px(164), px(160), px(48), color, 'center');
}

function createListNicknameStyle(color, px) {
    return Object.assign(
        createPositionedTextStyle(px(298), px(48), px(274), px(54), px(40), color, 'left'),
        createEllipsisStyle()
    );
}

function createSelfNicknameStyle(color, px) {
    return Object.assign(
        createPositionedTextStyle(px(319), px(53), px(274), px(54), px(40), color, 'left'),
        createEllipsisStyle()
    );
}

function createListScoreStyle(color, px) {
    return createRightPositionedTextStyle(px(40), px(44), px(210), px(61), px(42), color);
}

function createSelfScoreStyle(color, px) {
    return createRightPositionedTextStyle(px(40), px(49), px(291), px(63), px(42), color);
}

function createPositionedTextStyle(left, top, width, height, fontSize, color, textAlign) {
    return Object.assign(
        {
            position: 'absolute',
            left,
            top,
        },
        createTextStyle(width, height, fontSize, color, textAlign)
    );
}

function createRightPositionedTextStyle(right, top, width, height, fontSize, color) {
    return Object.assign(
        {
            position: 'absolute',
            right,
            top,
        },
        createTextStyle(width, height, fontSize, color, 'right')
    );
}

function createTextStyle(width, height, fontSize, color, textAlign) {
    return {
        width,
        height,
        lineHeight: height,
        fontSize,
        color,
        textAlign,
    };
}

function createEllipsisStyle() {
    return {
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    };
}

function createScaler(width) {
    const scale = width > 0 ? width / DESIGN_WIDTH : 1;
    return value => Math.max(1, Math.round(value * scale));
}
