let selfDataPromise;
// 本地 UI 联调开关。发布前必须改为 false；修改后只需刷新微信开发者工具。
const ENABLE_MOCK_DATA = false;
const MOCK_LOADING_DELAY_MS = 1200;

const MOCK_AVATARS = Object.freeze([
    'https://thirdwx.qlogo.cn/mmopen/vi_32/PiajxSqBRaEIj7R32HNb9q0FtsVCDZw10IU47Fcq9CXebxfpc1mGVyCOYDYULZoAjFCpO3LwXypWpeDtibx8ibVZ74OoJtSyZGoAgaheg88XCapaGX21yB4sw/132',
    'https://thirdwx.qlogo.cn/mmopen/vi_32/90FBvfOU7B8ThIY40YbGfrvANf5glSWM3Wsw0bJRtny0a2EhzlcpsQH4kT8ZmAxNXLmibkY1rUMJjiatBal2I4vg/132',
    'https://thirdwx.qlogo.cn/mmopen/vi_32/Q3auHgzwzM4KhB9u9ThNU023DNPR8rpU9jkJAWibrm7XvBrGicqfzX8YGUnIaxuGol6ia4TQd4klficB07cEnSAoRg/132',
    'https://thirdwx.qlogo.cn/mmopen/vi_32/Fdia5at9eFljcEwIqU4TLsc5OCr3SQxmZAw6LWNRlOAbCc88dLTwC6mlvb6NAOLrciborACbibrDZRoVcMTvAyF66G2NOgzY8oby5NC15esF40/132',
    'https://thirdwx.qlogo.cn/mmopen/vi_32/PiajxSqBRaEKHfNuciac7QElvQvYSuoL5IEHYmLQGicia5vQ7b2mWqbVaHLYBAhSMrZaSAM48h5Zf6p8LbbhQZ15iaPceCpFj9RrhSnbNqOWuicxNHjF2ibJQib8IQ/132',
    'https://thirdwx.qlogo.cn/mmopen/vi_32/1R7lHGBvwPSgx4uH1oof8BHfCeq2tcBoOmy5ZGic0kDrKKz6Xdo9J9dO4j8W3QNFTF4sXAshVUmv5p3E0F5pavsX7QUVbZzyE2BJTZias7tLY/132',
    'https://thirdwx.qlogo.cn/mmopen/vi_32/InknXfSAeGibK86FJI7RK7tcEXVDSjpQnibPqsv0f5LH959MBmote6ic1wJ0ribaiadrXYxyDn1WZfYGDPJian5libaFcc8zbvMiazPuy2hY3XribEOk/132',
]);

const MOCK_NICKNAMES = Object.freeze([
    '抓鹅大王', '欧气满满', '五连全出货', '名字特别特别长的抓鹅玩家', '鹅群管理员',
    '今天也要抓鹅', '金鹅在哪里', '默认头像测试', '一发入魂', '鹅鹅鹅',
    '攒券十连抽', '路过抓一只', '图鉴收集员', '重复鹅专业户', '运气守恒',
    '再来一抽', '抽卡不眨眼', '鹅场新人', '随缘出金', '还差最后一只',
    '保底也快乐', '小鹅快跑', '今晚必出货', '佛系收藏家', '抓鹅练习生',
    '今天不空军', '再抓亿只', '鹅运当头', '最后一张券', '我的测试账号',
]);

const MOCK_SCORES = Object.freeze([
    999, 876, 765, 699, 650, 612, 580, 543, 510, 488,
    455, 421, 399, 376, 350, 328, 305, 283, 260, 238,
    215, 193, 171, 149, 128, 106, 84, 63, 42, 30,
]);

export function loadFriendRank(key, selfOpenId) {
    if (ENABLE_MOCK_DATA) {
        console.warn('[WX Rank] using debug mock data');
        return new Promise((resolve) => {
            setTimeout(() => resolve(createMockEntries()), MOCK_LOADING_DELAY_MS);
        });
    }

    return Promise.all([
        getFriendCloudStorage(key),
        getSelfData(),
    ]).then(([response, self]) => normalizeEntries(
        response.data || [],
        self,
        key,
        selfOpenId
    ));
}

function createMockEntries() {
    return MOCK_NICKNAMES.map((nickname, index) => ({
        rank: index + 1,
        nickname,
        avatarUrl: index === 7 ? '' : MOCK_AVATARS[index % MOCK_AVATARS.length],
        score: MOCK_SCORES[index],
        updateTime: index,
        isSelf: index === MOCK_NICKNAMES.length - 1,
    }));
}

function getFriendCloudStorage(key) {
    return new Promise((resolve, reject) => {
        wx.getFriendCloudStorage({
            keyList: [key],
            success: resolve,
            fail: reject,
        });
    });
}

function getSelfData() {
    if (!selfDataPromise) {
        selfDataPromise = new Promise((resolve) => {
            wx.getUserInfo({
                openIdList: ['selfOpenId'],
                success: response => resolve((response.data || [])[0] || {}),
                fail: (error) => {
                    selfDataPromise = null;
                    console.error('[WX Rank] get self user info failed', error);
                    resolve({});
                },
            });
        });
    }

    return selfDataPromise;
}

function normalizeEntries(items, self, key, selfOpenId) {
    const entries = items
        .map(item => normalizeEntry(item, key, selfOpenId))
        .filter(item => item !== null);

    if (!entries.some(item => item.isSelf)) {
        entries.push(createSelfEntry(self));
    }

    return entries
        .sort(compareEntries)
        .map((item, index) => Object.assign(item, { rank: index + 1 }));
}

function createSelfEntry(self) {
    return {
        nickname: getNickname(self),
        avatarUrl: self.avatarUrl || '',
        score: 0,
        updateTime: 0,
        isSelf: true,
    };
}

function normalizeEntry(item, key, selfOpenId) {
    const storage = findStorage(item.KVDataList, key);
    if (!storage) {
        return null;
    }

    const rankValue = parseRankValue(storage.value);
    if (!rankValue) {
        return null;
    }

    return {
        nickname: getNickname(item),
        avatarUrl: item.avatarUrl || '',
        score: rankValue.score,
        updateTime: rankValue.updateTime,
        isSelf: isSelfEntry(item, selfOpenId),
    };
}

function findStorage(storageList, key) {
    if (!Array.isArray(storageList)) {
        return null;
    }

    return storageList.find(item => item && item.key === key) || null;
}

function parseRankValue(rawValue) {
    try {
        const value = JSON.parse(rawValue);
        if (typeof value === 'number' || typeof value === 'string') {
            const score = Number(value);
            return Number.isFinite(score) ? { score, updateTime: 0 } : null;
        }

        const wxGame = value && value.wxgame ? value.wxgame : value;
        const score = Number(wxGame && wxGame.score);

        if (!Number.isFinite(score)) {
            return null;
        }

        return {
            score,
            updateTime: Number(wxGame.update_time) || 0,
        };
    }
    catch (error) {
        const score = Number(rawValue);
        return Number.isFinite(score) ? { score, updateTime: 0 } : null;
    }
}

function isSelfEntry(item, selfOpenId) {
    return Boolean(item.openid && item.openid === selfOpenId);
}

function getNickname(user) {
    return user.nickname || user.nickName || '';
}

function compareEntries(left, right) {
    if (left.score !== right.score) {
        return right.score - left.score;
    }

    if (left.updateTime !== right.updateTime) {
        return left.updateTime - right.updateTime;
    }

    return left.nickname.localeCompare(right.nickname);
}
