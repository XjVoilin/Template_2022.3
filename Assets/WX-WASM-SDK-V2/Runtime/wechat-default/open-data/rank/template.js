const AVATAR_BACKGROUND = 'open-data/render/image/avatarBg.png';
const LOADING_GOOSE = 'open-data/render/image/loading.png';
const FIRST_RANK_BADGE = 'open-data/render/image/rankFirst.png';
const SECOND_RANK_BADGE = 'open-data/render/image/rankSecond.png';
const THIRD_RANK_BADGE = 'open-data/render/image/rankThird.png';

const FIRST_VARIANT = Object.freeze({
    item: 'firstRankItem',
    rankBadge: 'firstRankBadge',
    rankNumber: 'firstRankNumber',
    rankBadgeImage: FIRST_RANK_BADGE,
    avatarContainer: 'avatarContainer',
    nickname: 'firstNickname',
    score: 'firstScore',
});
const SECOND_VARIANT = Object.freeze({
    item: 'secondRankItem',
    rankBadge: 'secondRankBadge',
    rankNumber: 'secondRankNumber',
    rankBadgeImage: SECOND_RANK_BADGE,
    avatarContainer: 'avatarContainer',
    nickname: 'secondNickname',
    score: 'secondScore',
});
const THIRD_VARIANT = Object.freeze({
    item: 'thirdRankItem',
    rankBadge: 'thirdRankBadge',
    rankNumber: 'thirdRankNumber',
    rankBadgeImage: THIRD_RANK_BADGE,
    avatarContainer: 'avatarContainer',
    nickname: 'thirdNickname',
    score: 'thirdScore',
});
const NORMAL_VARIANT = Object.freeze({
    item: 'rankItem',
    rankBadge: 'rankBadge',
    rankNumber: 'rankNumber',
    avatarContainer: 'avatarContainer',
    nickname: 'nickname',
    score: 'score',
});
const SELF_VARIANT = Object.freeze({
    item: 'selfRankItem',
    rankBadge: 'selfRankBadge',
    rankNumber: 'selfRankNumber',
    avatarContainer: 'selfAvatarContainer',
    nickname: 'selfNickname',
    score: 'selfScore',
});

export default function createRankTemplate({ entries, scoreLabel }) {
    let xml = '<view class="container"><scrollview class="list" scrollY="true">';

    entries.slice(0, 30).forEach((entry) => {
        xml += createEntryTemplate(entry, scoreLabel, getRankVariant(entry.rank));
    });

    xml += '</scrollview>';

    const selfEntry = entries.find(entry => entry.isSelf);
    if (selfEntry) {
        xml += createEntryTemplate(selfEntry, scoreLabel, SELF_VARIANT);
    }

    xml += '</view>';
    return xml;
}

export function createLoadingTemplate() {
    return '<view class="loadingRoot"><image class="loadingGoose" src="'
        + LOADING_GOOSE
        + '"></image><text class="loadingText" value="数据加载中..."></text></view>';
}

function createEntryTemplate(entry, scoreLabel, variant) {
    let xml = `<view class="${variant.item}">`;
    if (variant.rankBadgeImage) {
        xml += `<image class="${variant.rankBadge}" src="${variant.rankBadgeImage}"></image>`;
    }
    else {
        xml += `<view class="${variant.rankBadge}">`;
        xml += `<text class="${variant.rankNumber}" value="${escapeAttribute(entry.rank)}"></text>`;
        xml += '</view>';
    }
    xml += `<view class="${variant.avatarContainer}">`;
    xml += '<view class="avatarOuterBackground"></view>';
    xml += `<image class="avatarInnerBackground" src="${AVATAR_BACKGROUND}"></image>`;
    if (entry.avatarUrl) {
        xml += `<image class="avatar" src="${escapeAttribute(entry.avatarUrl)}"></image>`;
    }
    xml += '</view>';
    xml += `<text class="${variant.nickname}" value="${escapeAttribute(entry.nickname)}"></text>`;
    xml += `<text class="${variant.score}" value="${escapeAttribute(formatScore(entry.score, scoreLabel))}"></text>`;
    xml += '</view>';
    return xml;
}

function getRankVariant(rank) {
    switch (rank) {
        case 1:
            return FIRST_VARIANT;
        case 2:
            return SECOND_VARIANT;
        case 3:
            return THIRD_VARIANT;
        default:
            return NORMAL_VARIANT;
    }
}

function formatScore(score, scoreLabel) {
    return `${score}${scoreLabel || ''}`;
}

function escapeAttribute(value) {
    return String(value == null ? '' : value)
        .replace(/&/g, '&amp;')
        .replace(/"/g, '&quot;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}
