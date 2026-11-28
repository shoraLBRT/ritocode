# Changelog

## [0.2.0](https://github.com/shoraLBRT/ritocode/compare/v0.1.0...v0.2.0) (2026-11-28)


### Features

* **admin:** add a system page showing what is deployed and how it is ([#311](https://github.com/shoraLBRT/ritocode/issues/311)) ([9e27cbd](https://github.com/shoraLBRT/ritocode/commit/9e27cbd6d3c4a2f72d01e1fac5c378d0e0253a12))
* **admin:** record server and browser errors and list them on /admin/errors ([#325](https://github.com/shoraLBRT/ritocode/issues/325)) ([f370d92](https://github.com/shoraLBRT/ritocode/commit/f370d923675b5296ed3d4c2b7005dc75e12ab985))
* **admin:** show per-task and per-card statistics for calibration ([#317](https://github.com/shoraLBRT/ritocode/issues/317)) ([e160adf](https://github.com/shoraLBRT/ritocode/commit/e160adf9d41cacd97ff3276f93d58979a6ae9db3))
* **admin:** show site traffic from Umami on the overview ([#322](https://github.com/shoraLBRT/ritocode/issues/322)) ([0f2cb91](https://github.com/shoraLBRT/ritocode/commit/0f2cb91fdeb3cc841436bf06ad9e89ff78894d44))
* **admin:** show usage over time on the admin overview ([#314](https://github.com/shoraLBRT/ritocode/issues/314)) ([88aa83f](https://github.com/shoraLBRT/ritocode/commit/88aa83f478f8c243ea37c7d9afd174b70ecb4b6c))
* **api:** open feedback issues as a GitHub App ([#288](https://github.com/shoraLBRT/ritocode/issues/288)) ([bcd63d4](https://github.com/shoraLBRT/ritocode/commit/bcd63d4e1f3b7ecd208904a43db0e3781ddd7b97))
* **auth:** let a learner export their data and delete their account ([#319](https://github.com/shoraLBRT/ritocode/issues/319)) ([144f98e](https://github.com/shoraLBRT/ritocode/commit/144f98e3b6de9ee087ae41bcd025eca32c3eba3a))
* **deploy:** check the server every five minutes and alert in Telegram ([#324](https://github.com/shoraLBRT/ritocode/issues/324)) ([e33b39a](https://github.com/shoraLBRT/ritocode/commit/e33b39a520f3cc157ee6f6e0f768a1a9340f6d49))
* **deploy:** serve a maintenance page when the site is down and during a release ([#320](https://github.com/shoraLBRT/ritocode/issues/320)) ([b50c7f0](https://github.com/shoraLBRT/ritocode/commit/b50c7f04344ead69f19fe5b35a5df02e0d1674e8))
* **frontend:** retitle the home page as a code review trainer ([#286](https://github.com/shoraLBRT/ritocode/issues/286)) ([13bae2a](https://github.com/shoraLBRT/ritocode/commit/13bae2abcacade55251c060e9c8dbb06ed1dfb1d))
* **frontend:** show study progress on the profile and count marks in the streak ([#291](https://github.com/shoraLBRT/ritocode/issues/291)) ([09c0c29](https://github.com/shoraLBRT/ritocode/commit/09c0c2924426f85ae7309d27f05a3fd5384e7c8b))
* **infra:** keep logs for 14 days and read them in Dozzle behind the admin session ([#327](https://github.com/shoraLBRT/ritocode/issues/327)) ([b841efe](https://github.com/shoraLBRT/ritocode/commit/b841efea3f0cd02a4e68b85e661e14b5b60b6874))
* mark a problem card studied on /problems ([#290](https://github.com/shoraLBRT/ritocode/issues/290)) ([76dc79f](https://github.com/shoraLBRT/ritocode/commit/76dc79fa5977aea8eabdc13c3e76f098d0d167b9))
* **web:** add link previews and structured data to the public pages ([#321](https://github.com/shoraLBRT/ritocode/issues/321)) ([eca0c82](https://github.com/shoraLBRT/ritocode/commit/eca0c82962c8029f50deaaadd57dd481cd4be040))
* **web:** add search console verification tags and IndexNow after a release ([#329](https://github.com/shoraLBRT/ritocode/issues/329)) ([7595e6a](https://github.com/shoraLBRT/ritocode/commit/7595e6a6d906cde9004ba80b5773f5426dd82847))
* **web:** answer 404 for unknown addresses and redirect to one canonical form ([#308](https://github.com/shoraLBRT/ritocode/issues/308)) ([ac1caf1](https://github.com/shoraLBRT/ritocode/commit/ac1caf18827aaf28bbe94a68d9fec0c355fd5e1c))
* **web:** prerender a page per problem card and per task ([#318](https://github.com/shoraLBRT/ritocode/issues/318)) ([6906a0a](https://github.com/shoraLBRT/ritocode/commit/6906a0a12a6e65e2315c36e306526a33e67dae07))


### Bug Fixes

* **frontend:** keep the class counts together at 375 px and reopen a card on a repeated hash ([#307](https://github.com/shoraLBRT/ritocode/issues/307)) ([edd4a26](https://github.com/shoraLBRT/ritocode/commit/edd4a2621ac563f585bcfca80641b6d37ba1b879))


### Performance Improvements

* **web:** hydrate the prerendered pages and split the bundle by route ([#326](https://github.com/shoraLBRT/ritocode/issues/326)) ([87998d9](https://github.com/shoraLBRT/ritocode/commit/87998d9902f0f0951cb277ba378cd0c87efe66aa))
* **web:** move the hydration data to its own file and preload the head's fonts ([#328](https://github.com/shoraLBRT/ritocode/issues/328)) ([0cbe90c](https://github.com/shoraLBRT/ritocode/commit/0cbe90c31e97dfa73170aa042f358994b780d89f))

## 0.1.0 (2026-11-17)


### Features

* **frontend:** accent the finding being treated in the treatment dock ([#246](https://github.com/shoraLBRT/ritocode/issues/246)) ([6b93494](https://github.com/shoraLBRT/ritocode/commit/6b93494f805563a1329fca697b124297abb29658))
* **frontend:** add a solved summary, sticky filters and eased filtering to the list pages ([#275](https://github.com/shoraLBRT/ritocode/issues/275)) ([5a67065](https://github.com/shoraLBRT/ritocode/commit/5a67065c279bee6e0d6d27b663925c8fce452119))
* **frontend:** add motion to the workspace's steps, picks and the result ([#276](https://github.com/shoraLBRT/ritocode/issues/276)) ([668792e](https://github.com/shoraLBRT/ritocode/commit/668792eddd98e5d3cd72a2aeb48c70413aceed5f))
* **frontend:** add motion tokens, interaction states and entrances ([#269](https://github.com/shoraLBRT/ritocode/issues/269)) ([b9258ca](https://github.com/shoraLBRT/ritocode/commit/b9258ca7defa0735c91d91f563958884262c437f))
* **frontend:** animate the home page's numbers and progress and make task rows whole targets ([#274](https://github.com/shoraLBRT/ritocode/issues/274)) ([835b5f5](https://github.com/shoraLBRT/ritocode/commit/835b5f542276b01906e6095013187eb350146713))
* **frontend:** hide the dock to read the code ([#249](https://github.com/shoraLBRT/ritocode/issues/249)) ([dc72d6f](https://github.com/shoraLBRT/ritocode/commit/dc72d6f58baaa54bd5753d2fa996e69fe87d4e97))
* **frontend:** move progress to a profile page in place of /progress ([#270](https://github.com/shoraLBRT/ritocode/issues/270)) ([9edc6b6](https://github.com/shoraLBRT/ritocode/commit/9edc6b68ce7cf565e99d36da61c56810890878e0))
* **frontend:** offer to try again or show the answer on an unsolved result ([#278](https://github.com/shoraLBRT/ritocode/issues/278)) ([49bb968](https://github.com/shoraLBRT/ritocode/commit/49bb968abccb0fb350479e88a9e942c52660d00e))
* **frontend:** one-row phone header with a menu, sticky header and a fuller footer ([#272](https://github.com/shoraLBRT/ritocode/issues/272)) ([43714e5](https://github.com/shoraLBRT/ritocode/commit/43714e52b45a7fafcc39b71c8a5b9ce4e17f2ad4))
* **frontend:** open a review card the way Diagnosis does, this task first ([#259](https://github.com/shoraLBRT/ritocode/issues/259)) ([41a6a63](https://github.com/shoraLBRT/ritocode/commit/41a6a639aeadaa8464b94a0857ab49fe0ff91110))
* **frontend:** raise the dock smoothly with the scroll ([#248](https://github.com/shoraLBRT/ritocode/issues/248)) ([5043561](https://github.com/shoraLBRT/ritocode/commit/5043561e59e9e15d99ccce643cb1199a202d1699))
* **frontend:** rebuild the admin area in the arena's style with an overview ([#271](https://github.com/shoraLBRT/ritocode/issues/271)) ([f76b89f](https://github.com/shoraLBRT/ritocode/commit/f76b89fdefb4ec7d995249978c75ea3a9b89074f))
* **frontend:** show a lane card's name and one conclusion, the rest on opening ([#257](https://github.com/shoraLBRT/ritocode/issues/257)) ([c761fb0](https://github.com/shoraLBRT/ritocode/commit/c761fb0407a64fcd8ea7b38678b6ab3dd6f3aab9))
* **frontend:** show the review's cards in three lanes with their treatments ([#251](https://github.com/shoraLBRT/ritocode/issues/251)) ([3f12e90](https://github.com/shoraLBRT/ritocode/commit/3f12e901d1685e67c1be6cb48e7de2c69c6e2c3d))
* **frontend:** skeleton loading, designed empty and error states, and a not-found page ([#273](https://github.com/shoraLBRT/ritocode/issues/273)) ([bcb89a1](https://github.com/shoraLBRT/ritocode/commit/bcb89a135dccc206ea2f6c956c19af3a5066af0d))
* send feedback without sign-in as a GitHub issue ([#250](https://github.com/shoraLBRT/ritocode/issues/250)) ([e6bf8d5](https://github.com/shoraLBRT/ritocode/commit/e6bf8d5bbc42676edeb49437fa6f43def8af3e59))
* show the join date and linked providers in the profile ([#277](https://github.com/shoraLBRT/ritocode/issues/277)) ([de3a4af](https://github.com/shoraLBRT/ritocode/commit/de3a4af2e74970ad3ba837a4b36ee17253584a31))


### Bug Fixes

* **frontend:** let the review's verdict own the colour and scroll its cards ([#255](https://github.com/shoraLBRT/ritocode/issues/255)) ([cf93715](https://github.com/shoraLBRT/ritocode/commit/cf93715026ebb28dbf0bb1ba5654a2847637d5ac))
