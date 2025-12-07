# Go-To-Market (GTM) Strategy: Chess Decoder

## Executive Summary
**Current Status**: Application is live. Core technology works with a "wow" effect during demos, but self-serve users struggle with image quality, leading to poor results (~10% accuracy historically) and high churn.
**Recent Improvements**: Accuracy potential increased to 50-90% with recent updates.
**Goal**: Bridge the gap between the demo experience and the user experience, prove value to a user base, and introduce monetization to cover LLM costs.

---

## 1. Problem Diagnosis
*   **The "Gap"**: Users expect magic but provide sub-optimal inputs (bad lighting, angles, glare).
*   **The Result**: 10% accuracy discourages users from fixing the output; they assume the tech is broken rather than the input being poor.
*   **The Opportunity**: The engine *can* deliver 50-90% accuracy. If we guide users to provide better inputs, the "wow" effect becomes reproducible.

## 2. Phased Strategy

### Phase 1: Activation & Value Proof (Current Focus)
**Objective**: Ensure new users successfully digitize one game and return.
**Metric**: Activation Rate (% of signups who save 1 valid PGN).

#### Tactics
1.  **"Hand-Holding" Onboard Experience**:
    *   **Education**: Before the first upload, show a modal or carousel: "How to take a perfect photo." Show examples of *Good* vs. *Bad* (glare, shadows, angle).
    *   **Feedback Loop**: If confidence is low (if detectable), prompt user: "This image might yield poor results due to [reason]. Try again?"
    *   **Sample Data**: Provide a "Try with a Sample Image" option so users see the 90% accuracy potential immediately, even if they don't have a scoresheet handy.

2.  **Trust Building**:
    *   Acknowledge imperfections. "We read 90% of moves correctly. You just need to verify the rest."
    *   Highlight the recent accuracy improvements in a "What's New" modal or banner.

3.  **Community Outreach (Grassroots)**:
    *   **Local Chess Club** (~20 active players every Sunday):
        *   **When to Launch**: After PG-181 (Hand-Holding Onboard Experience) is completed and tested
        *   **Approach**: Personal, in-person demos during club meetings
        *   **Advantage**: Direct feedback, can guide users through first upload, build trust
        *   **Strategy**: Show app during break, offer to process their scoresheets on the spot
        *   **Success Metric**: 10+ active users from club within 2 weeks
    *   **Viber Group** (30-40 chess players):
        *   **When to Launch**: After achieving 70%+ activation rate from local club users
        *   **Concern**: Data exploitation suspicions (competitors)
        *   **Approach**: 
            *   Emphasize privacy: "Your games stay private, you own your data"
            *   Open-source transparency (if applicable)
            *   Personal guarantee from you as a member
            *   Start with a small test group (5-10 trusted members)
        *   **Strategy**: Share success stories from local club, offer limited beta access
        *   **Success Metric**: 15+ active users from group within 1 month
    *   **Reddit** (r/chess or relevant subreddit):
        *   **When to Launch**: After achieving 60%+ activation rate overall and positive feedback from both local club and Viber group
        *   **Approach**: Respond to existing posts asking for this feature, create helpful demo video
        *   **Strategy**: 
            *   Find posts where users request this exact feature
            *   Share demo video showing 90% accuracy with good image
            *   Offer free access for first 50 users
            *   Be transparent about current limitations
        *   **Success Metric**: 50+ signups, 40%+ activation rate

### Phase 2: Monetization & Scaling
**Objective**: Sustainable revenue model to cover LLM API costs.
**Trigger**: When we have a cohort of users consistently using the app with:
- **Activation Rate**: ≥60% (users who save 1 valid PGN after signup)
- **Retention Rate**: ≥30% (users who upload a second game within 30 days)
- **Total Active Users**: ≥50 users consistently using the app
- **Processing Completion Rate**: ≥40% (users who complete and export at least one game)

#### Pricing Strategy
*   **Freemium Model**:
    *   **Free**: X games per month (e.g., 3-5). Sufficient for a casual tournament player.
    *   **Premium**: Unlimited uploads, cloud storage of history, advanced export options, priority support.
*   **Justification**: "We use advanced AI (LLMs) which costs money per game. Support the development."

#### Cost Control
*   Optimize prompts to reduce token usage.
*   Cache results where possible.
*   Restrict free tier to lower-cost models if viable, reserve high-cost/high-accuracy models for Premium if there is a distinction.

---

## 3. Product Roadmap Alignment (Immediate Steps)

> **See [GTM Implementation Status](./GTM-Implementation-Status.md) for detailed issue alignment and readiness checklists.**
To support Phase 1, the following product features are critical:

1.  **UX/UI for Image Quality** (PG-181 - **HIGHEST PRIORITY**):
    *   Implement "Best Practices" guide in the upload flow.
    *   Add validation warnings if the image is blurry or low contrast (if technically feasible on client-side).
    *   **GTM Impact**: Directly addresses activation rate by improving first-upload success
2.  **Accuracy Transparency**:
    *   Display confidence levels or "Review Needed" markers on generated moves.
    *   **GTM Impact**: Builds trust, sets proper expectations
3.  **Retention Features**:
    *   Project History (PG-156) – allows users to come back and finish editing later.
    *   **GTM Impact**: Improves retention rate by allowing users to complete games later
    *   PGN Update & Completion Flag (PG-178) – tracks when users successfully complete games.
    *   **GTM Impact**: Enables tracking of processing completion rate (critical KPI)
4.  **Activation Tracking**:
    *   Implement analytics to track activation rate metrics
    *   Track quality guide views, sample image usage, first upload success
    *   **GTM Impact**: Provides data to measure Phase 1 success and readiness for Phase 2

### Readiness Checklist for Outreach Launch

#### Local Chess Club Launch (First Stage)
- [ ] PG-181 (Hand-Holding Onboard Experience) completed and tested
- [ ] Sample image feature working and demonstrates 90% accuracy
- [ ] Quality guide shows good/bad examples clearly
- [ ] Image quality feedback loop functional
- [ ] Can track basic metrics (signups, uploads, completions)
- [ ] Personal demo script prepared
- [ ] Privacy/data handling message ready

#### Viber Group Launch (Second Stage)
- [ ] 70%+ activation rate achieved from local club users
- [ ] 3+ positive testimonials/feedback from club members
- [ ] Privacy policy and data handling clearly documented
- [ ] Can handle 15-20 concurrent users without performance issues
- [ ] Support process in place for user questions
- [ ] Success stories ready to share

#### Reddit Launch (Third Stage)
- [ ] 60%+ overall activation rate
- [ ] 30%+ retention rate (users uploading second game)
- [ ] 40%+ processing completion rate
- [ ] Demo video showing full workflow (2-3 minutes)
- [ ] Clear value proposition and limitations documented
- [ ] Support system can handle 50+ new users
- [ ] Monitoring and analytics dashboard functional

## 4. Key Performance Indicators (KPIs)

### Phase 1 Metrics (Activation & Value Proof)
1.  **Activation Rate**: % of signups who save 1 valid PGN (Target: ≥60%)
2.  **Upload Success Rate**: % of uploads that result in a saved game (Target: ≥80%)
3.  **First Upload Accuracy**: Average accuracy of first upload (Target: ≥50%)
4.  **Sample Image Usage**: % of first-time users who try sample image (Target: ≥40%)
5.  **Quality Guide Completion**: % of first-time users who view quality guide (Target: ≥80%)
6.  **Retention**: % of users who upload a second game within 30 days (Target: ≥30%)
7.  **Processing Completion Rate**: % of users who export at least one game (Target: ≥40%)

### Phase 2 Metrics (Monetization)
1.  **Conversion Rate**: % of free users who upgrade to Premium (Target: ≥5%)
2.  **Monthly Active Users (MAU)**: Total users active in last 30 days
3.  **Average Games per User**: Monthly average games processed per active user
4.  **Cost per User**: Average LLM cost per user per month
5.  **Revenue per User**: Average revenue per paying user per month

### Tracking Implementation
- **Backend**: Track user signups, game uploads, processing completions, exports
- **Analytics**: Implement event tracking for:
  - User signup
  - First upload attempt
  - Quality guide viewed/dismissed
  - Sample image used
  - Game saved
  - Game exported (Lichess/Chess.com)
  - Second game uploaded
- **Database**: Use `ProcessingCompleted` flag (PG-178) to track completion rate
- **Project History** (PG-156): Track user engagement and return visits

