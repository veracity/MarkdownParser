# Overview

# Quick start
Code exmaple:
```javascript
export const App = () => (
  <Provider store={store}>
    <ConnectedRouter history={history}>
      <div>
        <HeaderContainer/>
        <Switch>
          <Route exact path="/" component={HomeRoute}/>
          <Route exact path="/developer" component={DeveloperRoute}/>
          <Route path="/developer/article" component={ArticleRoute}/>
          <Route component={NotFoundRoute}/>
        </Switch>
        <Footer/>
      </div>
    </ConnectedRouter>
  </Provider>
);
export default App;

```

```python
export const loadArticleById = articleId => {
  if (!articleId) throw new Error('articleId argument required');
  const timeout = getSimulatedTimeoutDuration();
  console.log('Article load', articleId, timeout);
  return new Promise((res, rej) => {
    setTimeout(() => {
      res(mockArticle);
    }, timeout);
  });
};
```

# Tutorial

