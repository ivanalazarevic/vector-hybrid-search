// A small invented article set in the style of the BBC News dataset (business, entertainment,
// politics, sport, tech). The mock search ranks over it, so any query gives plausible results.
//
// Awkward cases planted here on purpose:
//  - biz-005 has no category and no source
//  - pol-001 has a very long title
//  - biz-004 contains "&" and "<" in its text
//  - several articles mention "interest" or "rates" only once: weak matches, where the two
//    engines disagree (see rankLexical), which gives the compare page a partial overlap

export interface MockArticle {
  articleId: string;
  title: string;
  text: string;
  category?: string;
  source?: string;
}

export const CORPUS: readonly MockArticle[] = [
  {
    articleId: "biz-001",
    title: "Bank holds interest rates as housing market cools",
    text: "The Bank of England has kept interest rates on hold at 4.75% for a fifth month, saying a slowdown in the housing market has eased the pressure on inflation. Economists said rates were now likely to stay unchanged until the summer, although a further rise in oil prices could still force the Bank to act. Mortgage lenders reported that approvals fell by a tenth in January, and several said they expected interest rates to have peaked.",
    category: "business",
    source: "BBC News",
  },
  {
    articleId: "biz-002",
    title: "Oil prices climb as cold weather lifts demand",
    text: "Oil prices rose above $50 a barrel after forecasts of a cold spell in the United States raised fears about supplies of heating fuel. Analysts said the rise would add to inflation and could delay any cut in interest rates. Airlines and hauliers warned that higher fuel costs would be passed on to customers within weeks.",
    category: "business",
    source: "BBC News",
  },
  {
    articleId: "biz-003",
    title: "Car maker to cut 3,000 jobs after profits fall",
    text: "One of Europe's biggest car makers is to cut 3,000 jobs after annual profits fell by a third. The company blamed weak demand, the strong euro and the rising price of steel. Unions said they would fight compulsory redundancies at the firm's plants and called for urgent talks with the government. The firm also faces higher interest charges on its debt.",
    category: "business",
    source: "BBC News",
  },
  {
    articleId: "biz-004",
    title: "Drug firm lifts R&D budget as rivals merge",
    text: "The drug firm said spending on R&D would rise to 18% of sales, up from <15% a year ago, as it tries to refill its pipeline of new medicines. Rivals have chosen mergers & acquisitions instead; one deal announced this week is worth $12bn. Analysts said research budgets across the industry were under pressure, and shares in the firm rose 2% in early trading.",
    category: "business",
    source: "BBC News",
  },
  {
    articleId: "biz-005",
    title: "Fixed-rate mortgage deals pulled as lenders expect rates to rise",
    text: "Several lenders have withdrawn their cheapest fixed-rate mortgage deals in the expectation that interest rates will rise again this year. Brokers said borrowers who wanted certainty over their repayments should move quickly. Rates on two-year deals have already gone up by a quarter of a point since December, and interest in longer fixed terms is growing.",
  },
  {
    articleId: "biz-006",
    title: "High street sales slip as shoppers feel the squeeze",
    text: "Retail sales fell for a second month as higher mortgage costs and fuel bills left shoppers with less to spend. The British Retail Consortium said clothing and furniture were worst hit. Some economists said the figures made another rise in interest rates unlikely, while others pointed to strong wage growth.",
    category: "business",
    source: "BBC News",
  },
  {
    articleId: "pol-001",
    title:
      "Chancellor insists there is no black hole in the public finances as opposition parties step up their attacks on tax and spending plans ahead of the general election expected in the spring",
    text: "The chancellor has rejected claims that taxes will have to rise after the election, insisting his spending plans are affordable. Opposition parties said independent forecasts showed a gap of several billion pounds. The row comes as all parties prepare their manifestos for an election widely expected in May.",
    category: "politics",
    source: "BBC News",
  },
  {
    articleId: "pol-002",
    title: "Parties clash over identity cards plan",
    text: "Plans for compulsory identity cards have been attacked by opposition MPs, who said the scheme would cost billions and do little to prevent terrorism. Ministers said the cards would help tackle fraud and illegal working. The bill is expected to run out of parliamentary time before the election.",
    category: "politics",
    source: "BBC News",
  },
  {
    articleId: "pol-003",
    title: "Election turnout fears as postal voting expands",
    text: "Election officials have warned that a big expansion of postal voting could lead to fraud unless safeguards are tightened. The government wants to increase turnout, which fell to 59% at the last general election. Critics said the election could be undermined if voters lost confidence in the system.",
    category: "politics",
    source: "BBC News",
  },
  {
    articleId: "pol-004",
    title: "Council tax rises to stay below 5%, ministers say",
    text: "Council tax bills in England will rise by less than 5% on average this year, ministers have said, after extra money was found for local authorities. Opposition parties called it a pre-election bribe and warned that bills would jump again next year once spending limits were lifted. Business rates are not affected.",
    category: "politics",
    source: "BBC News",
  },
  {
    articleId: "spo-001",
    title: "Late goal sends holders through in Champions League",
    text: "A goal three minutes from time sent the holders into the quarter-finals of the Champions League. The manager praised his players' patience after a first half of few chances. The draw for the next round of the Champions League takes place on Friday.",
    category: "sport",
    source: "BBC Sport",
  },
  {
    articleId: "spo-002",
    title: "Captain ruled out of Six Nations opener",
    text: "England's captain will miss the opening match of the Six Nations with a knee injury picked up in training. The coach said he hoped to have him back for the second match in a fortnight. England have won only two of their last six matches.",
    category: "sport",
    source: "BBC Sport",
  },
  {
    articleId: "spo-003",
    title: "Sprinter cleared to race after doping hearing",
    text: "A British sprinter has been cleared to compete at the European indoor championships after a disciplinary panel accepted that a banned stimulant came from a contaminated supplement. The athlete said the hearing had been the hardest week of his career. Officials warned other athletes to check every product they take.",
    category: "sport",
    source: "BBC Sport",
  },
  {
    articleId: "spo-004",
    title: "Record fee as striker joins league leaders",
    text: "The league leaders have paid a club record fee for a striker who scored 19 goals last season. The transfer was completed minutes before the deadline. The manager said the signing showed the club's ambition to win the league and go further in the Champions League next season. The fee was agreed in euros at current exchange rates.",
    category: "sport",
    source: "BBC Sport",
  },
  {
    articleId: "tec-001",
    title: "Broadband users pass six million as prices fall",
    text: "More than six million UK homes now have broadband after a round of price cuts by the biggest internet providers. The regulator said competition was working but that rural areas were still being left behind. Faster connections are changing how people use the net, with music downloads and online games growing fastest.",
    category: "tech",
    source: "BBC News",
  },
  {
    articleId: "tec-002",
    title: "Mobile phones to get faster net access",
    text: "Mobile phone operators are to upgrade their 3G networks to offer net access at speeds close to home broadband. Trials start this summer in three cities. Analysts said operators needed new services to win back the billions they spent on licences, but doubted that customers would pay more for speed alone. Early interest from business users has been strong.",
    category: "tech",
    source: "BBC News",
  },
  {
    articleId: "tec-003",
    title: "Games console sales hit record over Christmas",
    text: "Sales of games consoles and handhelds reached a record over Christmas, industry figures show. Shortages of the most popular handheld meant some shops sold out within days. Publishers said online games played over broadband were the fastest growing part of the market.",
    category: "tech",
    source: "BBC News",
  },
  {
    articleId: "tec-004",
    title: "Search engines race to index desktop files",
    text: "The biggest search engines are racing to offer tools that search the files on a user's own PC as well as the web. The programs index e-mail, documents and browsing history so they can be found in seconds. Privacy groups warned that shared computers could expose personal files to other users.",
    category: "tech",
    source: "BBC News",
  },
  {
    articleId: "ent-001",
    title: "British film wins top prize at awards ceremony",
    text: "A low-budget British film has won best film at this year's awards, beating two Hollywood epics. Its director, who also won best director, said the film had been turned down by every major studio. The awards are seen as a guide to next month's Oscars.",
    category: "entertainment",
    source: "BBC News",
  },
  {
    articleId: "ent-002",
    title: "Chart-topping band announce summer festival dates",
    text: "The band behind the year's biggest-selling album are to headline three festivals this summer. Tickets go on sale on Friday. The singer said the band wanted to play the new songs to as many people as possible before returning to the studio in the autumn. Promoters expect interest in tickets to be high.",
    category: "entertainment",
    source: "BBC News",
  },
  {
    articleId: "ent-003",
    title: "Box office record for animated film sequel",
    text: "An animated sequel has broken the record for the biggest opening weekend for an animated film, taking $108m in the United States. The studio has already confirmed a third film. Critics were less impressed, and several said the story had lost the charm of the original.",
    category: "entertainment",
    source: "BBC News",
  },
];
